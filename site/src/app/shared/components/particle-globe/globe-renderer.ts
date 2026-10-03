// A sphere of points with a pulse rolling across it, drawn with three.js.
// Kept apart from the component so three.js only ever loads in the browser.

export interface GlobeRenderer {
  setColor(hex: string): void;
  dispose(): void;
}

const VERTEX = /* glsl */ `
  uniform float uTime;
  uniform float uSize;
  attribute float aSeed;
  varying float vGlow;
  varying float vFacing;
  void main() {
    vec3 p = position;
    // two pulses: one sweeping from the north pole, one ring orbiting the equator
    float lat = acos(clamp(p.y, -1.0, 1.0));
    float wave = sin(lat * 6.0 - uTime * 1.4);
    float ring = exp(-pow((atan(p.z, p.x) - mod(uTime * 0.5, 6.2832) + 3.1416) * 2.0, 2.0)) * (1.0 - abs(p.y));
    vGlow = smoothstep(0.75, 1.0, wave) * 0.8 + ring * 0.9 + 0.12 * aSeed;
    vec4 mv = modelViewMatrix * vec4(p * (1.0 + 0.02 * smoothstep(0.85, 1.0, wave)), 1.0);
    vFacing = clamp(normalize(normalMatrix * p).z * 0.5 + 0.5, 0.0, 1.0);
    gl_PointSize = uSize * (0.6 + vGlow) * (2.6 / -mv.z);
    gl_Position = projectionMatrix * mv;
  }
`;

const FRAGMENT = /* glsl */ `
  uniform vec3 uColor;
  uniform vec3 uAccent;
  varying float vGlow;
  varying float vFacing;
  void main() {
    vec2 c = gl_PointCoord - 0.5;
    if (dot(c, c) > 0.25) discard;
    float alpha = (0.18 + vGlow * 0.75) * mix(0.25, 1.0, vFacing);
    vec3 color = mix(uColor, uAccent, smoothstep(0.7, 1.2, vGlow));
    gl_FragColor = vec4(color, alpha);
  }
`;

export async function createGlobeRenderer(canvas: HTMLCanvasElement, host: HTMLElement, color: string): Promise<GlobeRenderer> {
  const THREE = await import('three');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)').matches;

  const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: true, powerPreference: 'low-power' });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x000000, 0);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 20);
  camera.position.set(0, 0, 4.2);

  // evenly spread points: a Fibonacci sphere
  const COUNT = 2400;
  const positions = new Float32Array(COUNT * 3);
  const seeds = new Float32Array(COUNT);
  const golden = Math.PI * (3 - Math.sqrt(5));
  for (let i = 0; i < COUNT; i++) {
    const y = 1 - (i / (COUNT - 1)) * 2;
    const r = Math.sqrt(1 - y * y);
    const theta = golden * i;
    positions.set([Math.cos(theta) * r, y, Math.sin(theta) * r], i * 3);
    seeds[i] = Math.random();
  }
  const geometry = new THREE.BufferGeometry();
  geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
  geometry.setAttribute('aSeed', new THREE.BufferAttribute(seeds, 1));

  const material = new THREE.ShaderMaterial({
    vertexShader: VERTEX,
    fragmentShader: FRAGMENT,
    transparent: true,
    depthWrite: false,
    uniforms: {
      uTime: { value: 0 },
      uSize: { value: 2.2 * renderer.getPixelRatio() },
      uColor: { value: new THREE.Color(color) },
      uAccent: { value: new THREE.Color('#e5412f') },
    },
  });
  const globe = new THREE.Points(geometry, material);
  globe.rotation.set(0.35, 0, 0.18);
  scene.add(globe);

  const resize = () => {
    const w = host.clientWidth, h = host.clientHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    renderer.render(scene, camera);
  };

  let raf = 0;
  let visible = true;
  const start = performance.now();
  const loop = (t: number) => {
    raf = requestAnimationFrame(loop);
    if (!visible || document.visibilityState !== 'visible') return;
    const s = (t - start) / 1000;
    material.uniforms['uTime'].value = s;
    globe.rotation.y = s * 0.06;
    renderer.render(scene, camera);
  };

  const io = new IntersectionObserver(([entry]) => (visible = entry.isIntersecting));
  io.observe(host);
  const ro = new ResizeObserver(resize);
  ro.observe(host);
  resize();
  if (reduced) {
    material.uniforms['uTime'].value = 2.0; // one still frame, mid-pulse
    renderer.render(scene, camera);
  } else {
    raf = requestAnimationFrame(loop);
  }

  return {
    setColor(hex: string) {
      material.uniforms['uColor'].value.set(hex);
      if (reduced) renderer.render(scene, camera);
    },
    dispose() {
      cancelAnimationFrame(raf);
      io.disconnect();
      ro.disconnect();
      geometry.dispose();
      material.dispose();
      renderer.dispose();
    },
  };
}
