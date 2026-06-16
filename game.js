const canvas = document.getElementById("game");
const ctx = canvas.getContext("2d");

const timeEl = document.getElementById("time");
const levelEl = document.getElementById("level");
const killsEl = document.getElementById("kills");
const hpBar = document.getElementById("hpBar");
const xpBar = document.getElementById("xpBar");
const startPanel = document.getElementById("startPanel");
const endPanel = document.getElementById("endPanel");
const upgradePanel = document.getElementById("upgradePanel");
const upgradeChoices = document.getElementById("upgradeChoices");
const endStats = document.getElementById("endStats");
const startButton = document.getElementById("startButton");
const restartButton = document.getElementById("restartButton");

const keys = new Set();
const world = { width: 3600, height: 3600 };
const camera = { x: world.width / 2, y: world.height / 2 };
const projection = { tilt: 0.56, anchorY: 455 };

let player;
let enemies;
let gems;
let projectiles;
let slashEffects;
let props;
let game;
let lastTime = 0;

const upgrades = [
  {
    name: "Twin Daggers",
    desc: "Fire one extra blade every attack cycle.",
    apply: () => player.shots += 1
  },
  {
    name: "Quick Hands",
    desc: "Attack cooldown becomes shorter.",
    apply: () => player.attackCooldown = Math.max(0.18, player.attackCooldown * 0.82)
  },
  {
    name: "Sharpened Steel",
    desc: "Projectile damage increases by 35%.",
    apply: () => player.damage = Math.ceil(player.damage * 1.35)
  },
  {
    name: "Long Step",
    desc: "Move faster through the arena.",
    apply: () => player.speed += 30
  },
  {
    name: "Magnet Charm",
    desc: "Pull experience gems from farther away.",
    apply: () => player.pickupRange += 58
  },
  {
    name: "Iron Heart",
    desc: "Heal now and increase max HP.",
    apply: () => {
      player.maxHp += 18;
      player.hp = Math.min(player.maxHp, player.hp + 34);
    }
  }
];

function resetGame() {
  player = {
    x: world.width / 2,
    y: world.height / 2,
    radius: 24,
    speed: 245,
    hp: 100,
    maxHp: 100,
    xp: 0,
    nextXp: 8,
    level: 1,
    attackTimer: 0,
    attackCooldown: 0.62,
    shots: 1,
    damage: 14,
    pickupRange: 95,
    hurtTimer: 0,
    facing: 1
  };

  enemies = [];
  gems = [];
  projectiles = [];
  slashEffects = [];
  props = createProps();
  game = {
    running: true,
    pausedForUpgrade: false,
    elapsed: 0,
    spawnTimer: 0,
    kills: 0,
    wave: 1
  };

  lastTime = performance.now();
  startPanel.classList.add("hidden");
  endPanel.classList.add("hidden");
  upgradePanel.classList.add("hidden");
  requestAnimationFrame(loop);
}

function createProps() {
  const list = [];
  for (let i = 0; i < 90; i++) {
    list.push({
      x: 160 + Math.random() * (world.width - 320),
      y: 160 + Math.random() * (world.height - 320),
      type: Math.random() > 0.54 ? "pillar" : "stone",
      size: 18 + Math.random() * 34,
      shade: 0.72 + Math.random() * 0.28
    });
  }
  return list;
}

function loop(now) {
  const dt = Math.min(0.033, (now - lastTime) / 1000 || 0);
  lastTime = now;

  if (game.running && !game.pausedForUpgrade) update(dt);
  draw();

  if (game.running) requestAnimationFrame(loop);
}

function update(dt) {
  game.elapsed += dt;
  game.wave = 1 + Math.floor(game.elapsed / 35);
  player.hurtTimer = Math.max(0, player.hurtTimer - dt);

  movePlayer(dt);
  spawnEnemies(dt);
  updateEnemies(dt);
  updateProjectiles(dt);
  updateGems(dt);
  updateEffects(dt);
  autoAttack(dt);
  updateHud();

  if (player.hp <= 0) endRun();
}

function movePlayer(dt) {
  let dx = 0;
  let dy = 0;
  if (keys.has("w") || keys.has("arrowup")) dy -= 1;
  if (keys.has("s") || keys.has("arrowdown")) dy += 1;
  if (keys.has("a") || keys.has("arrowleft")) dx -= 1;
  if (keys.has("d") || keys.has("arrowright")) dx += 1;

  if (dx || dy) {
    const len = Math.hypot(dx, dy);
    dx /= len;
    dy /= len;
    player.x = clamp(player.x + dx * player.speed * dt, 80, world.width - 80);
    player.y = clamp(player.y + dy * player.speed * dt, 80, world.height - 80);
    player.facing = dx < -0.05 ? -1 : dx > 0.05 ? 1 : player.facing;
  }

  camera.x += (player.x - camera.x) * Math.min(1, dt * 8);
  camera.y += (player.y - camera.y) * Math.min(1, dt * 8);
}

function spawnEnemies(dt) {
  game.spawnTimer -= dt;
  if (game.spawnTimer > 0) return;

  const count = 2 + Math.min(8, game.wave);
  for (let i = 0; i < count; i++) spawnEnemy();
  game.spawnTimer = Math.max(0.18, 1.35 - game.wave * 0.08);
}

function spawnEnemy() {
  const angle = Math.random() * Math.PI * 2;
  const distance = 760 + Math.random() * 380;
  const x = clamp(player.x + Math.cos(angle) * distance, 50, world.width - 50);
  const y = clamp(player.y + Math.sin(angle) * distance, 50, world.height - 50);
  const brute = Math.random() < Math.min(0.1 + game.wave * 0.025, 0.34);

  enemies.push({
    x,
    y,
    radius: brute ? 30 : 21,
    hp: brute ? 58 + game.wave * 9 : 24 + game.wave * 4,
    maxHp: brute ? 58 + game.wave * 9 : 24 + game.wave * 4,
    speed: brute ? 82 + game.wave * 3 : 118 + game.wave * 4,
    damage: brute ? 17 : 9,
    xp: brute ? 4 : 1,
    type: brute ? "brute" : "shade",
    wobble: Math.random() * Math.PI * 2
  });
}

function updateEnemies(dt) {
  for (const enemy of enemies) {
    const dx = player.x - enemy.x;
    const dy = player.y - enemy.y;
    const dist = Math.hypot(dx, dy) || 1;
    enemy.wobble += dt * 5;
    const sway = Math.sin(enemy.wobble) * 0.25;
    enemy.x += ((dx / dist) + (-dy / dist) * sway) * enemy.speed * dt;
    enemy.y += ((dy / dist) + (dx / dist) * sway) * enemy.speed * dt;

    if (dist < enemy.radius + player.radius) {
      player.hp -= enemy.damage * dt;
      player.hurtTimer = 0.12;
    }
  }
}

function autoAttack(dt) {
  player.attackTimer -= dt;
  if (player.attackTimer > 0 || enemies.length === 0) return;

  const targets = [...enemies]
    .sort((a, b) => distance(player, a) - distance(player, b))
    .slice(0, player.shots);

  targets.forEach((target, index) => {
    const angle = Math.atan2(target.y - player.y, target.x - player.x) + (index - (targets.length - 1) / 2) * 0.14;
    projectiles.push({
      x: player.x,
      y: player.y,
      vx: Math.cos(angle) * 650,
      vy: Math.sin(angle) * 650,
      radius: 9,
      damage: player.damage,
      life: 1.15,
      spin: Math.random() * Math.PI
    });
    slashEffects.push({ x: player.x, y: player.y, angle, life: 0.16, maxLife: 0.16 });
  });

  player.attackTimer = player.attackCooldown;
}

function updateProjectiles(dt) {
  for (const shot of projectiles) {
    shot.x += shot.vx * dt;
    shot.y += shot.vy * dt;
    shot.life -= dt;
    shot.spin += dt * 18;

    for (const enemy of enemies) {
      if (enemy.hp <= 0) continue;
      if (Math.hypot(enemy.x - shot.x, enemy.y - shot.y) < enemy.radius + shot.radius) {
        enemy.hp -= shot.damage;
        shot.life = 0;
        slashEffects.push({ x: enemy.x, y: enemy.y, angle: Math.atan2(shot.vy, shot.vx), life: 0.22, maxLife: 0.22 });
        if (enemy.hp <= 0) killEnemy(enemy);
        break;
      }
    }
  }

  projectiles = projectiles.filter(shot => shot.life > 0);
  enemies = enemies.filter(enemy => enemy.hp > 0);
}

function killEnemy(enemy) {
  game.kills += 1;
  gems.push({
    x: enemy.x,
    y: enemy.y,
    value: enemy.xp,
    radius: 9 + Math.min(9, enemy.xp * 2),
    bob: Math.random() * Math.PI * 2
  });
}

function updateGems(dt) {
  for (const gem of gems) {
    gem.bob += dt * 6;
    const dx = player.x - gem.x;
    const dy = player.y - gem.y;
    const dist = Math.hypot(dx, dy) || 1;
    if (dist < player.pickupRange) {
      const pull = 240 + (player.pickupRange - dist) * 7;
      gem.x += (dx / dist) * pull * dt;
      gem.y += (dy / dist) * pull * dt;
    }

    if (dist < player.radius + gem.radius) {
      player.xp += gem.value;
      gem.collected = true;
      if (player.xp >= player.nextXp) levelUp();
    }
  }
  gems = gems.filter(gem => !gem.collected);
}

function levelUp() {
  player.xp -= player.nextXp;
  player.level += 1;
  player.nextXp = Math.floor(player.nextXp * 1.32 + 6);
  game.pausedForUpgrade = true;
  showUpgradeChoices();
}

function showUpgradeChoices() {
  upgradeChoices.innerHTML = "";
  const choices = shuffle([...upgrades]).slice(0, 3);

  for (const upgrade of choices) {
    const button = document.createElement("button");
    button.className = "upgrade-card";
    button.type = "button";
    button.innerHTML = `<strong>${upgrade.name}</strong><span>${upgrade.desc}</span>`;
    button.addEventListener("click", () => {
      upgrade.apply();
      upgradePanel.classList.add("hidden");
      game.pausedForUpgrade = false;
      lastTime = performance.now();
    });
    upgradeChoices.appendChild(button);
  }

  upgradePanel.classList.remove("hidden");
}

function updateEffects(dt) {
  slashEffects.forEach(effect => effect.life -= dt);
  slashEffects = slashEffects.filter(effect => effect.life > 0);
}

function endRun() {
  game.running = false;
  endStats.textContent = `You lasted ${formatTime(game.elapsed)}, reached level ${player.level}, and defeated ${game.kills} enemies.`;
  endPanel.classList.remove("hidden");
}

function draw() {
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  drawSky();
  drawArena();

  const drawables = [
    ...props.map(item => ({ item, type: "prop", y: item.y })),
    ...gems.map(item => ({ item, type: "gem", y: item.y })),
    ...projectiles.map(item => ({ item, type: "projectile", y: item.y })),
    ...enemies.map(item => ({ item, type: "enemy", y: item.y })),
    { item: player, type: "player", y: player.y },
    ...slashEffects.map(item => ({ item, type: "effect", y: item.y }))
  ].sort((a, b) => a.y - b.y);

  drawables.forEach(drawable => {
    if (drawable.type === "prop") drawProp(drawable.item);
    if (drawable.type === "gem") drawGem(drawable.item);
    if (drawable.type === "projectile") drawProjectile(drawable.item);
    if (drawable.type === "enemy") drawEnemy(drawable.item);
    if (drawable.type === "player") drawPlayer(drawable.item);
    if (drawable.type === "effect") drawSlash(drawable.item);
  });
}

function drawSky() {
  const gradient = ctx.createLinearGradient(0, 0, 0, canvas.height);
  gradient.addColorStop(0, "#151b22");
  gradient.addColorStop(0.45, "#222525");
  gradient.addColorStop(1, "#111516");
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  ctx.fillStyle = "rgba(226, 195, 110, 0.12)";
  ctx.beginPath();
  ctx.arc(canvas.width * 0.78, canvas.height * 0.18, 84, 0, Math.PI * 2);
  ctx.fill();
}

function drawArena() {
  ctx.save();
  ctx.translate(canvas.width / 2, projection.anchorY);
  ctx.scale(1, projection.tilt);
  ctx.translate(-camera.x, -camera.y);

  ctx.fillStyle = "#2e332d";
  ctx.fillRect(0, 0, world.width, world.height);

  ctx.strokeStyle = "rgba(245, 241, 232, 0.07)";
  ctx.lineWidth = 3;
  for (let x = 0; x <= world.width; x += 160) {
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x, world.height);
    ctx.stroke();
  }
  for (let y = 0; y <= world.height; y += 160) {
    ctx.beginPath();
    ctx.moveTo(0, y);
    ctx.lineTo(world.width, y);
    ctx.stroke();
  }

  ctx.strokeStyle = "rgba(226, 195, 110, 0.22)";
  ctx.lineWidth = 12;
  ctx.strokeRect(24, 24, world.width - 48, world.height - 48);
  ctx.restore();
}

function drawShadow(entity, width, alpha = 0.24) {
  const p = project(entity.x, entity.y);
  ctx.fillStyle = `rgba(0, 0, 0, ${alpha})`;
  ctx.beginPath();
  ctx.ellipse(p.x, p.y + 16, width, width * 0.28, 0, 0, Math.PI * 2);
  ctx.fill();
}

function drawPlayer(ply) {
  const p = project(ply.x, ply.y);
  drawShadow(ply, 30, 0.32);

  ctx.save();
  ctx.translate(p.x, p.y);
  ctx.scale(ply.facing, 1);
  ctx.fillStyle = ply.hurtTimer > 0 ? "#fff2df" : "#d9e7e6";
  ctx.beginPath();
  ctx.ellipse(0, -18, 18, 31, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = "#31464c";
  ctx.fillRect(-16, -8, 32, 30);
  ctx.fillStyle = "#e2c36e";
  ctx.fillRect(12, -8, 30, 6);
  ctx.fillStyle = "#191817";
  ctx.beginPath();
  ctx.arc(7, -23, 3, 0, Math.PI * 2);
  ctx.fill();
  ctx.restore();
}

function drawEnemy(enemy) {
  const p = project(enemy.x, enemy.y);
  drawShadow(enemy, enemy.radius * 1.05, 0.28);

  ctx.save();
  ctx.translate(p.x, p.y);
  const body = enemy.type === "brute" ? "#7d3848" : "#4d5968";
  const trim = enemy.type === "brute" ? "#f2a65a" : "#77d1d8";
  ctx.fillStyle = body;
  ctx.beginPath();
  ctx.ellipse(0, -enemy.radius * 0.76, enemy.radius * 0.82, enemy.radius * 1.12, 0, 0, Math.PI * 2);
  ctx.fill();
  ctx.fillStyle = trim;
  ctx.beginPath();
  ctx.arc(-enemy.radius * 0.28, -enemy.radius, 3, 0, Math.PI * 2);
  ctx.arc(enemy.radius * 0.28, -enemy.radius, 3, 0, Math.PI * 2);
  ctx.fill();

  const hpWidth = enemy.radius * 1.6;
  ctx.fillStyle = "rgba(0, 0, 0, 0.35)";
  ctx.fillRect(-hpWidth / 2, -enemy.radius * 2.05, hpWidth, 4);
  ctx.fillStyle = "#d84e4e";
  ctx.fillRect(-hpWidth / 2, -enemy.radius * 2.05, hpWidth * Math.max(0, enemy.hp / enemy.maxHp), 4);
  ctx.restore();
}

function drawGem(gem) {
  const p = project(gem.x, gem.y);
  const bob = Math.sin(gem.bob) * 4;
  drawShadow(gem, gem.radius, 0.18);
  ctx.save();
  ctx.translate(p.x, p.y - 18 + bob);
  ctx.rotate(Math.PI / 4);
  ctx.fillStyle = gem.value > 1 ? "#92e0b8" : "#53b7d0";
  ctx.fillRect(-gem.radius * 0.55, -gem.radius * 0.55, gem.radius * 1.1, gem.radius * 1.1);
  ctx.restore();
}

function drawProjectile(shot) {
  const p = project(shot.x, shot.y);
  ctx.save();
  ctx.translate(p.x, p.y - 18);
  ctx.rotate(shot.spin);
  ctx.fillStyle = "#f3efd8";
  ctx.fillRect(-14, -3, 28, 6);
  ctx.fillStyle = "#e2c36e";
  ctx.fillRect(2, -2, 12, 4);
  ctx.restore();
}

function drawSlash(effect) {
  const p = project(effect.x, effect.y);
  const t = effect.life / effect.maxLife;
  ctx.save();
  ctx.translate(p.x, p.y - 22);
  ctx.rotate(effect.angle);
  ctx.strokeStyle = `rgba(242, 166, 90, ${t})`;
  ctx.lineWidth = 5;
  ctx.beginPath();
  ctx.arc(0, 0, 34 * (1.1 - t), -0.5, 0.6);
  ctx.stroke();
  ctx.restore();
}

function drawProp(prop) {
  const p = project(prop.x, prop.y);
  drawShadow(prop, prop.size * 0.8, 0.2);
  ctx.save();
  ctx.translate(p.x, p.y);
  if (prop.type === "pillar") {
    ctx.fillStyle = `rgba(${Math.floor(110 * prop.shade)}, ${Math.floor(122 * prop.shade)}, ${Math.floor(114 * prop.shade)}, 1)`;
    ctx.fillRect(-prop.size * 0.34, -prop.size * 1.55, prop.size * 0.68, prop.size * 1.55);
    ctx.fillStyle = "#323735";
    ctx.fillRect(-prop.size * 0.52, -prop.size * 1.68, prop.size * 1.04, prop.size * 0.18);
  } else {
    ctx.fillStyle = "#4b504b";
    ctx.beginPath();
    ctx.ellipse(0, -prop.size * 0.18, prop.size * 0.64, prop.size * 0.42, 0.15, 0, Math.PI * 2);
    ctx.fill();
  }
  ctx.restore();
}

function project(x, y) {
  return {
    x: canvas.width / 2 + (x - camera.x),
    y: projection.anchorY + (y - camera.y) * projection.tilt
  };
}

function updateHud() {
  if (!player || !game) {
    timeEl.textContent = "00:00";
    levelEl.textContent = "1";
    killsEl.textContent = "0";
    hpBar.style.width = "100%";
    xpBar.style.width = "0%";
    return;
  }

  timeEl.textContent = formatTime(game.elapsed);
  levelEl.textContent = player.level;
  killsEl.textContent = game.kills;
  hpBar.style.width = `${clamp((player.hp / player.maxHp) * 100, 0, 100)}%`;
  xpBar.style.width = `${clamp((player.xp / player.nextXp) * 100, 0, 100)}%`;
}

function formatTime(totalSeconds) {
  const minutes = Math.floor(totalSeconds / 60).toString().padStart(2, "0");
  const seconds = Math.floor(totalSeconds % 60).toString().padStart(2, "0");
  return `${minutes}:${seconds}`;
}

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function shuffle(items) {
  for (let i = items.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [items[i], items[j]] = [items[j], items[i]];
  }
  return items;
}

window.addEventListener("keydown", event => {
  keys.add(event.key.toLowerCase());
  if (["ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight", " "].includes(event.key)) {
    event.preventDefault();
  }
});

window.addEventListener("keyup", event => {
  keys.delete(event.key.toLowerCase());
});

startButton.addEventListener("click", resetGame);
restartButton.addEventListener("click", resetGame);
updateHud();
drawSky();
drawArena();
