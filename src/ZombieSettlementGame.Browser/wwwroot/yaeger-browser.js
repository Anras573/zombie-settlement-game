/**
 * Yaeger browser interop module — WebGL 2.0 rendering backend.
 *
 * All exports are imported by Yaeger.Browser via [JSImport("functionName", "yaeger-browser")].
 * Load this module once at startup with JSHost.ImportAsync("yaeger-browser", "./yaeger-browser.js").
 */

// ---------------------------------------------------------------------------
// WebGL state
// ---------------------------------------------------------------------------

let canvas;
let gl;
let shaderProgram;
let vao;
let vbo;
let ebo;
let viewProjUniformLocation;
let textureUniformLocation;
let whiteTexture;

const MAX_QUADS = 1000;
const VERTICES_PER_QUAD = 4;
const FLOATS_PER_VERTEX = 9; // pos(3) + uv(2) + color(4)
const INDICES_PER_QUAD = 6;

/** url -> WebGLTexture, or null while loading, or whiteTexture after a 404. */
const textureCache = new Map();

const VERTEX_SHADER_SOURCE = `#version 300 es
layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec2 aTexCoord;
layout(location = 2) in vec4 aColor;

uniform mat4 uViewProj;

out vec2 vTexCoord;
out vec4 vColor;

void main() {
    gl_Position = uViewProj * vec4(aPosition, 1.0);
    vTexCoord = aTexCoord;
    vColor = aColor;
}`;

const FRAGMENT_SHADER_SOURCE = `#version 300 es
precision mediump float;

in vec2 vTexCoord;
in vec4 vColor;
out vec4 FragColor;

uniform sampler2D uTexture;

void main() {
    FragColor = texture(uTexture, vTexCoord) * vColor;
}`;

function compileShader(type, source) {
    const shader = gl.createShader(type);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
        const log = gl.getShaderInfoLog(shader);
        gl.deleteShader(shader);
        throw new Error(`[Yaeger] Shader compile error: ${log}`);
    }
    return shader;
}

function createShaderProgram(vertSrc, fragSrc) {
    const vert = compileShader(gl.VERTEX_SHADER, vertSrc);
    const frag = compileShader(gl.FRAGMENT_SHADER, fragSrc);
    const prog = gl.createProgram();
    gl.attachShader(prog, vert);
    gl.attachShader(prog, frag);
    gl.linkProgram(prog);
    gl.deleteShader(vert);
    gl.deleteShader(frag);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
        throw new Error(`[Yaeger] Shader link error: ${gl.getProgramInfoLog(prog)}`);
    }
    return prog;
}

function generateQuadIndices(maxQuads) {
    const indices = new Uint32Array(maxQuads * INDICES_PER_QUAD);
    for (let i = 0; i < maxQuads; i++) {
        const v = i * VERTICES_PER_QUAD;
        const idx = i * INDICES_PER_QUAD;
        // Winding: TR(0), BR(1), TL(3)  +  BR(1), BL(2), TL(3)
        indices[idx + 0] = v;
        indices[idx + 1] = v + 1;
        indices[idx + 2] = v + 3;
        indices[idx + 3] = v + 1;
        indices[idx + 4] = v + 2;
        indices[idx + 5] = v + 3;
    }
    return indices;
}

function createWhiteTexture() {
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(
        gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0,
        gl.RGBA, gl.UNSIGNED_BYTE,
        new Uint8Array([255, 255, 255, 255])
    );
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    return tex;
}

/**
 * Returns the WebGLTexture for the given URL, starting an async load on first access.
 * Returns whiteTexture (1×1 white) while loading or when url is empty/null.
 */
function getOrLoadTexture(url) {
    if (!url) return whiteTexture;

    if (textureCache.has(url)) {
        return textureCache.get(url) || whiteTexture;
    }

    textureCache.set(url, null); // mark as in-flight

    const img = new Image();
    img.onload = () => {
        if (!gl) return;
        const tex = gl.createTexture();
        gl.bindTexture(gl.TEXTURE_2D, tex);
        // Flip Y so row 0 is at the bottom, matching StbImageSharp FlipVerticallyOnLoad
        // used by the desktop Texture loader. Without this, sprites render upside-down.
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, img);
        gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
        gl.generateMipmap(gl.TEXTURE_2D);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
        gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
        textureCache.set(url, tex);
    };
    img.onerror = () => {
        console.warn(`[Yaeger] Failed to load texture: ${url}`);
        textureCache.set(url, whiteTexture);
    };
    img.src = url;

    return whiteTexture;
}

// ---------------------------------------------------------------------------
// Input state  (unchanged from Canvas 2D version)
// ---------------------------------------------------------------------------

const pressedKeys = new Set();
let mouseX = 0;
let mouseY = 0;
let scrollDelta = 0;
const mouseButtons = new Set();
let activePrimaryPointerId;
const PIXELS_PER_LINE = 16;
const WHEEL_EVENT_OPTIONS = { passive: false };
const POINTER_EVENT_OPTIONS = { passive: false };

let resizeCanvasHandler;
let keyDownHandler;
let keyUpHandler;
let pointerDownHandler;
let pointerMoveHandler;
let pointerUpHandler;
let pointerCancelHandler;
let wheelHandler;
let blurHandler;
let contextMenuHandler;

function clearInputState() {
    pressedKeys.clear();
    mouseButtons.clear();
    scrollDelta = 0;
    activePrimaryPointerId = undefined;
}

function normalizeWheelDeltaToPixels(e) {
    if (e.deltaMode === WheelEvent.DOM_DELTA_LINE) {
        return e.deltaY * PIXELS_PER_LINE;
    }
    if (e.deltaMode === WheelEvent.DOM_DELTA_PAGE) {
        const pageHeight = canvas?.clientHeight || window.innerHeight || 0;
        return e.deltaY * pageHeight;
    }
    return e.deltaY;
}

function setupInputListeners() {
    resizeCanvasHandler = () => {
        if (!canvas) return;
        const dpr = window.devicePixelRatio || 1;
        canvas.width = Math.round(canvas.clientWidth * dpr);
        canvas.height = Math.round(canvas.clientHeight * dpr);
        if (gl) gl.viewport(0, 0, canvas.width, canvas.height);
    };

    keyDownHandler = (e) => { if (e.code) pressedKeys.add(e.code); };
    keyUpHandler = (e) => { if (e.code) pressedKeys.delete(e.code); };

    pointerMoveHandler = (e) => {
        if (!canvas) return;
        if (e.pointerType !== 'mouse' && e.pointerId !== activePrimaryPointerId) return;
        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;
        if (e.pointerType !== 'mouse') e.preventDefault();
    };

    pointerDownHandler = (e) => {
        if (!canvas) return;
        if (e.pointerType === 'mouse') {
            const rect = canvas.getBoundingClientRect();
            mouseX = e.clientX - rect.left;
            mouseY = e.clientY - rect.top;
            mouseButtons.add(e.button);
            return;
        }
        if (activePrimaryPointerId === undefined) activePrimaryPointerId = e.pointerId;
        if (e.pointerId !== activePrimaryPointerId) return;
        const rect = canvas.getBoundingClientRect();
        mouseX = e.clientX - rect.left;
        mouseY = e.clientY - rect.top;
        mouseButtons.add(0);
        if (canvas.setPointerCapture) canvas.setPointerCapture(e.pointerId);
        e.preventDefault();
    };

    pointerUpHandler = (e) => {
        if (!canvas) return;
        if (e.pointerType === 'mouse') { mouseButtons.delete(e.button); return; }
        if (e.pointerId !== activePrimaryPointerId) return;
        mouseButtons.delete(0);
        activePrimaryPointerId = undefined;
        if (canvas.hasPointerCapture?.(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
        e.preventDefault();
    };

    pointerCancelHandler = (e) => {
        if (!canvas || e.pointerType === 'mouse' || e.pointerId !== activePrimaryPointerId) return;
        mouseButtons.delete(0);
        activePrimaryPointerId = undefined;
        if (canvas.hasPointerCapture?.(e.pointerId)) canvas.releasePointerCapture(e.pointerId);
        e.preventDefault();
    };

    wheelHandler = (e) => { scrollDelta += normalizeWheelDeltaToPixels(e); e.preventDefault(); };
    blurHandler = () => clearInputState();
    contextMenuHandler = (e) => e.preventDefault();

    resizeCanvasHandler();
    window.addEventListener('resize', resizeCanvasHandler);
    window.addEventListener('keydown', keyDownHandler);
    window.addEventListener('keyup', keyUpHandler);
    canvas.addEventListener('pointermove', pointerMoveHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('pointerdown', pointerDownHandler, POINTER_EVENT_OPTIONS);
    window.addEventListener('pointerup', pointerUpHandler, POINTER_EVENT_OPTIONS);
    window.addEventListener('pointercancel', pointerCancelHandler, POINTER_EVENT_OPTIONS);
    canvas.addEventListener('wheel', wheelHandler, WHEEL_EVENT_OPTIONS);
    window.addEventListener('blur', blurHandler);
    canvas.addEventListener('contextmenu', contextMenuHandler);
}

function removeInputListeners() {
    if (resizeCanvasHandler) {
        window.removeEventListener('resize', resizeCanvasHandler);
        resizeCanvasHandler = undefined;
    }
    if (keyDownHandler) {
        window.removeEventListener('keydown', keyDownHandler);
        keyDownHandler = undefined;
    }
    if (keyUpHandler) {
        window.removeEventListener('keyup', keyUpHandler);
        keyUpHandler = undefined;
    }
    if (pointerMoveHandler && canvas) {
        canvas.removeEventListener('pointermove', pointerMoveHandler, POINTER_EVENT_OPTIONS);
        pointerMoveHandler = undefined;
    }
    if (pointerDownHandler && canvas) {
        canvas.removeEventListener('pointerdown', pointerDownHandler, POINTER_EVENT_OPTIONS);
        pointerDownHandler = undefined;
    }
    if (pointerUpHandler) {
        window.removeEventListener('pointerup', pointerUpHandler, POINTER_EVENT_OPTIONS);
        pointerUpHandler = undefined;
    }
    if (pointerCancelHandler) {
        window.removeEventListener('pointercancel', pointerCancelHandler, POINTER_EVENT_OPTIONS);
        pointerCancelHandler = undefined;
    }
    if (wheelHandler && canvas) {
        canvas.removeEventListener('wheel', wheelHandler, WHEEL_EVENT_OPTIONS);
        wheelHandler = undefined;
    }
    if (blurHandler) {
        window.removeEventListener('blur', blurHandler);
        blurHandler = undefined;
    }
    if (contextMenuHandler && canvas) {
        canvas.removeEventListener('contextmenu', contextMenuHandler);
        contextMenuHandler = undefined;
    }
}

// ---------------------------------------------------------------------------
// Exported API
// ---------------------------------------------------------------------------

/**
 * Initialises the WebGL 2.0 rendering context, compiles shaders, allocates GPU
 * buffers, and registers input event listeners for the given canvas element.
 * Must be called once before any other export.
 */
export function initWebGL(canvasId) {
    disposeCanvas();

    canvas = document.getElementById(canvasId);
    if (!canvas) throw new Error(`[Yaeger] Canvas element '#${canvasId}' not found.`);

    gl = canvas.getContext('webgl2');
    if (!gl) throw new Error('[Yaeger] WebGL 2.0 is not supported in this browser.');

    canvas.style.touchAction = 'none';

    // Shaders
    shaderProgram = createShaderProgram(VERTEX_SHADER_SOURCE, FRAGMENT_SHADER_SOURCE);
    viewProjUniformLocation = gl.getUniformLocation(shaderProgram, 'uViewProj');
    textureUniformLocation = gl.getUniformLocation(shaderProgram, 'uTexture');

    gl.useProgram(shaderProgram);
    gl.uniform1i(textureUniformLocation, 0);
    // Identity view-projection as default
    gl.uniformMatrix4fv(viewProjUniformLocation, false, [
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1,
    ]);

    // VBO — dynamic, large enough for one full batch
    vbo = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bufferData(
        gl.ARRAY_BUFFER,
        MAX_QUADS * VERTICES_PER_QUAD * FLOATS_PER_VERTEX * 4, // bytes
        gl.DYNAMIC_DRAW
    );

    // EBO — static index buffer, shared across all batches
    ebo = gl.createBuffer();
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ebo);
    gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, generateQuadIndices(MAX_QUADS), gl.STATIC_DRAW);

    // VAO — captures attribute layout once
    vao = gl.createVertexArray();
    gl.bindVertexArray(vao);
    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, ebo);

    const stride = FLOATS_PER_VERTEX * 4; // bytes per vertex
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 3, gl.FLOAT, false, stride, 0);        // position (3 floats)
    gl.enableVertexAttribArray(1);
    gl.vertexAttribPointer(1, 2, gl.FLOAT, false, stride, 3 * 4);    // texcoord (2 floats)
    gl.enableVertexAttribArray(2);
    gl.vertexAttribPointer(2, 4, gl.FLOAT, false, stride, 5 * 4);    // color    (4 floats)

    gl.bindVertexArray(null);

    // 1×1 white fallback texture (used for empty paths and while async loads are in flight)
    whiteTexture = createWhiteTexture();

    gl.enable(gl.BLEND);
    gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);

    setupInputListeners();
}

/**
 * Clears the colour buffer and updates the WebGL viewport if the canvas was resized.
 */
export function clearFrame() {
    if (!gl) return;

    const dpr = window.devicePixelRatio || 1;
    const w = Math.round(canvas.clientWidth * dpr);
    const h = Math.round(canvas.clientHeight * dpr);
    if (canvas.width !== w || canvas.height !== h) {
        canvas.width = w;
        canvas.height = h;
        gl.viewport(0, 0, w, h);
    }

    gl.clearColor(0, 0, 0, 1);
    gl.clear(gl.COLOR_BUFFER_BIT);
}

/**
 * Updates the view-projection matrix uniform used for all subsequent draw calls.
 * <paramref name="matrixBytes"/> is a Uint8Array of 64 bytes — the raw memory of a
 * System.Numerics.Matrix4x4 (16 × IEEE 754 float, row-major). The bytes are
 * reinterpreted as a Float32Array and passed with transpose=false, matching the
 * convention used by the desktop OpenGL renderer.
 */
export function setViewProjection(matrixBytes) {
    if (!gl || !shaderProgram) return;
    gl.useProgram(shaderProgram);
    // Copy to a fresh aligned buffer so Float32Array view is always valid.
    const aligned = new Uint8Array(64);
    aligned.set(matrixBytes.subarray(0, 64));
    gl.uniformMatrix4fv(viewProjUniformLocation, false, new Float32Array(aligned.buffer));
}

/**
 * Renders one texture batch.  <paramref name="vertexBytes"/> is a Uint8Array containing
 * the raw bytes of the C# float vertex scratch buffer (9 floats × 4 bytes per vertex,
 * 4 vertices per quad); only the first <c>quadCount * 4 * 9 * 4</c> bytes are uploaded.
 * The texture for <paramref name="textureUrl"/> is loaded asynchronously on first use;
 * a 1×1 white fallback is used while it is in flight so tint colour still renders.
 */
export function drawBatch(textureUrl, vertexBytes, quadCount) {
    if (!gl || quadCount <= 0) return;

    const texture = getOrLoadTexture(textureUrl);
    const byteCount = quadCount * VERTICES_PER_QUAD * FLOATS_PER_VERTEX * 4;

    gl.useProgram(shaderProgram);
    gl.bindVertexArray(vao);

    gl.bindBuffer(gl.ARRAY_BUFFER, vbo);
    // Upload only the live portion. bufferSubData with a Uint8Array uses byte counts.
    gl.bufferSubData(gl.ARRAY_BUFFER, 0, vertexBytes, 0, byteCount);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texture);

    gl.drawElements(gl.TRIANGLES, quadCount * INDICES_PER_QUAD, gl.UNSIGNED_INT, 0);

    gl.bindVertexArray(null);
}

/**
 * Releases all WebGL resources, removes input event listeners, and resets state.
 */
export function disposeCanvas() {
    if (gl) {
        textureCache.forEach((tex) => {
            if (tex && tex !== whiteTexture) gl.deleteTexture(tex);
        });
        textureCache.clear();
        if (whiteTexture) { gl.deleteTexture(whiteTexture); whiteTexture = null; }
        if (vao) { gl.deleteVertexArray(vao); vao = null; }
        if (vbo) { gl.deleteBuffer(vbo); vbo = null; }
        if (ebo) { gl.deleteBuffer(ebo); ebo = null; }
        if (shaderProgram) { gl.deleteProgram(shaderProgram); shaderProgram = null; }
        gl = null;
    }

    removeInputListeners();
    clearInputState();
    mouseX = 0;
    mouseY = 0;
    canvas = null;
}

export function isKeyPressed(key) {
    return pressedKeys.has(key);
}

export function isMouseButtonPressed(button) {
    return mouseButtons.has(button);
}

export function getMouseX() {
    return mouseX;
}

export function getMouseY() {
    return mouseY;
}

export function getMouseXNdc() {
    if (!canvas || canvas.clientWidth === 0) return 0;
    return (mouseX / canvas.clientWidth) * 2 - 1;
}

export function getMouseYNdc() {
    if (!canvas || canvas.clientHeight === 0) return 0;
    return 1 - (mouseY / canvas.clientHeight) * 2;
}

export function getAndResetScrollDelta() {
    const delta = scrollDelta;
    scrollDelta = 0;
    return delta;
}
