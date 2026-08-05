const canvas = document.getElementById('gameCanvas');
const context = canvas.getContext('2d');
const serverStatus = document.getElementById('serverStatus');

const TILE_SIZE = 30;
const MAP_COLS = canvas.width / TILE_SIZE;
const MAP_ROWS = canvas.height / TILE_SIZE;

const LOOP_SECONDS = 10;
const REWIND_SECONDS = 2.2;
const FPS_REFERENCE = 60;
const LOOP_FRAME_LIMIT = Math.floor(LOOP_SECONDS * FPS_REFERENCE);
const REWIND_FRAME_LIMIT = Math.floor(REWIND_SECONDS * FPS_REFERENCE);

const player = {
  x: canvas.width / 2,
  y: canvas.height / 2,
  startX: canvas.width / 2,
  startY: canvas.height / 2,
  radius: 16,
  speed: 240,
};

const padA = { x: 220, y: canvas.height / 2, radius: 24 };
const padB = { x: canvas.width - 220, y: canvas.height / 2, radius: 24 };

const core = {
  x: canvas.width / 2,
  y: canvas.height / 2,
  radius: 14,
  collected: false,
};

const timeLock = {
  progress: 0,
  unlockSeconds: 1.4,
  unlocked: false,
};

let temporalStability = 5;
const terrainMap = createTerrainMap();

const keys = new Set();
let lastTime = performance.now();
let paradoxScore = 0;
let loopsCreated = 0;

const ghosts = [];
const loopRecording = [];
const rewindTrail = [];
const syncCooldowns = new Map();
let ghostSequence = 0;

const enemies = createPatrolEnemies();

function distance(a, b) {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function randomRange(min, max) {
  return min + Math.random() * (max - min);
}

function tileCenter(col, row) {
  return {
    x: col * TILE_SIZE + TILE_SIZE / 2,
    y: row * TILE_SIZE + TILE_SIZE / 2,
  };
}

function createTerrainMap() {
  const map = Array.from({ length: MAP_ROWS }, () => Array(MAP_COLS).fill(0));

  for (let row = 0; row < MAP_ROWS; row += 1) {
    for (let col = 0; col < MAP_COLS; col += 1) {
      const border = row === 0 || col === 0 || row === MAP_ROWS - 1 || col === MAP_COLS - 1;
      if (border) {
        map[row][col] = 1;
      }
    }
  }

  for (let col = 4; col < MAP_COLS - 4; col += 6) {
    for (let row = 2; row < MAP_ROWS - 2; row += 1) {
      if (row === Math.floor(MAP_ROWS / 2) || row === Math.floor(MAP_ROWS / 2) + 1) {
        continue;
      }
      map[row][col] = 1;
    }
  }

  for (let row = 4; row < MAP_ROWS - 4; row += 5) {
    for (let col = 2; col < MAP_COLS - 2; col += 1) {
      if (col % 7 === 0 || col % 7 === 1) {
        continue;
      }
      map[row][col] = 1;
    }
  }

  for (let row = 6; row <= 8; row += 1) {
    for (let col = 13; col <= 18; col += 1) {
      if (map[row][col] === 0) {
        map[row][col] = 2;
      }
    }
  }

  for (let row = 11; row <= 13; row += 1) {
    for (let col = 5; col <= 9; col += 1) {
      if (map[row][col] === 0) {
        map[row][col] = 2;
      }
    }
  }

  for (let row = 2; row < MAP_ROWS - 2; row += 1) {
    for (let col = 2; col < MAP_COLS - 2; col += 1) {
      if (map[row][col] === 0 && (row + col) % 9 === 0) {
        map[row][col] = 3;
      }
    }
  }

  clearArea(map, player.startX, player.startY, 2);
  clearArea(map, canvas.width / 2, canvas.height / 2, 2);
  return map;
}

function clearArea(map, centerX, centerY, radiusTiles) {
  const centerCol = Math.floor(centerX / TILE_SIZE);
  const centerRow = Math.floor(centerY / TILE_SIZE);
  for (let row = centerRow - radiusTiles; row <= centerRow + radiusTiles; row += 1) {
    for (let col = centerCol - radiusTiles; col <= centerCol + radiusTiles; col += 1) {
      if (row > 0 && row < MAP_ROWS - 1 && col > 0 && col < MAP_COLS - 1) {
        map[row][col] = 0;
      }
    }
  }
}

function getTileTypeAt(x, y) {
  const col = clamp(Math.floor(x / TILE_SIZE), 0, MAP_COLS - 1);
  const row = clamp(Math.floor(y / TILE_SIZE), 0, MAP_ROWS - 1);
  return terrainMap[row][col];
}

function isSolidTile(tileType) {
  return tileType === 1;
}

function isWaterTile(tileType) {
  return tileType === 2;
}

function canOccupyCircle(x, y, radius) {
  const points = [
    [x, y],
    [x - radius, y],
    [x + radius, y],
    [x, y - radius],
    [x, y + radius],
  ];

  for (const [px, py] of points) {
    if (isSolidTile(getTileTypeAt(px, py))) {
      return false;
    }
  }
  return true;
}

function getRandomWalkablePoint() {
  for (let attempts = 0; attempts < 300; attempts += 1) {
    const col = Math.floor(randomRange(2, MAP_COLS - 2));
    const row = Math.floor(randomRange(2, MAP_ROWS - 2));
    if (!isSolidTile(terrainMap[row][col]) && !isWaterTile(terrainMap[row][col])) {
      return tileCenter(col, row);
    }
  }

  return { x: canvas.width / 2, y: canvas.height / 2 };
}

function createPatrolEnemies() {
  const patrolLines = [
    [tileCenter(3, 3), tileCenter(10, 3), tileCenter(10, 8), tileCenter(3, 8)],
    [tileCenter(21, 4), tileCenter(28, 4), tileCenter(28, 10), tileCenter(21, 10)],
    [tileCenter(12, 12), tileCenter(19, 12), tileCenter(19, 16), tileCenter(12, 16)],
  ];

  return patrolLines.map((waypoints, index) => ({
    id: index,
    x: waypoints[0].x,
    y: waypoints[0].y,
    radius: 13,
    speed: 84 + index * 12,
    waypoints,
    waypointIndex: 1,
    hitCooldown: 0,
  }));
}

function movePlayerToStart() {
  player.x = player.startX;
  player.y = player.startY;
}

function setupNewCycle() {
  const padAPoint = getRandomWalkablePoint();
  const padBPoint = getRandomWalkablePoint();
  const corePoint = getRandomWalkablePoint();

  padA.x = padAPoint.x;
  padA.y = padAPoint.y;
  padB.x = padBPoint.x;
  padB.y = padBPoint.y;

  core.x = corePoint.x;
  core.y = corePoint.y;
  core.collected = false;

  timeLock.progress = 0;
  timeLock.unlocked = false;
}

function createGhostFromRecording() {
  if (loopRecording.length < 20) {
    return;
  }

  ghosts.push({
    id: ghostSequence,
    frames: loopRecording.slice(),
    frameIndex: 0,
    x: loopRecording[0].x,
    y: loopRecording[0].y,
    radius: player.radius,
  });
  ghostSequence += 1;

  loopsCreated += 1;
  loopRecording.length = 0;
  rewindTrail.length = 0;
  movePlayerToStart();
}

window.addEventListener('keydown', (event) => keys.add(event.key.toLowerCase()));
window.addEventListener('keyup', (event) => keys.delete(event.key.toLowerCase()));

window.addEventListener('keydown', (event) => {
  if ((event.code === 'Space' || event.key.toLowerCase() === 't') && !event.repeat) {
    event.preventDefault();
    createGhostFromRecording();
  }
});

function actorOnPad(actor, pad) {
  return distance(actor, pad) <= actor.radius + pad.radius - 4;
}

function updateGhosts() {
  for (let i = ghosts.length - 1; i >= 0; i -= 1) {
    const ghost = ghosts[i];
    if (ghost.frameIndex >= ghost.frames.length) {
      syncCooldowns.delete(ghost.id);
      ghosts.splice(i, 1);
      continue;
    }

    const frame = ghost.frames[ghost.frameIndex];
    ghost.x = frame.x;
    ghost.y = frame.y;
    ghost.frameIndex += 1;
  }
}

function updateRewindTrail() {
  rewindTrail.push({ x: player.x, y: player.y });
  if (rewindTrail.length > REWIND_FRAME_LIMIT) {
    rewindTrail.shift();
  }
}

function updateLoopRecording() {
  loopRecording.push({ x: player.x, y: player.y });
  if (loopRecording.length > LOOP_FRAME_LIMIT) {
    loopRecording.shift();
  }
}

function applyRewind() {
  if (rewindTrail.length < 2) {
    return false;
  }

  rewindTrail.pop();
  const frame = rewindTrail[rewindTrail.length - 1];
  if (canOccupyCircle(frame.x, frame.y, player.radius)) {
    player.x = frame.x;
    player.y = frame.y;
  }
  return true;
}

function updateEnemies(deltaTime) {
  for (const enemy of enemies) {
    if (enemy.hitCooldown > 0) {
      enemy.hitCooldown -= deltaTime;
    }

    const target = enemy.waypoints[enemy.waypointIndex];
    const directionX = target.x - enemy.x;
    const directionY = target.y - enemy.y;
    const distanceToTarget = Math.hypot(directionX, directionY);

    if (distanceToTarget < 4) {
      enemy.waypointIndex = (enemy.waypointIndex + 1) % enemy.waypoints.length;
      continue;
    }

    const step = enemy.speed * deltaTime;
    const ratio = Math.min(step / distanceToTarget, 1);
    const nextX = enemy.x + directionX * ratio;
    const nextY = enemy.y + directionY * ratio;

    if (canOccupyCircle(nextX, nextY, enemy.radius)) {
      enemy.x = nextX;
      enemy.y = nextY;
    } else {
      enemy.waypointIndex = (enemy.waypointIndex + 1) % enemy.waypoints.length;
    }

    if (enemy.hitCooldown <= 0 && distance(enemy, player) <= enemy.radius + player.radius - 2) {
      temporalStability = Math.max(0, temporalStability - 1);
      paradoxScore = Math.max(0, paradoxScore - 2);
      enemy.hitCooldown = 1.1;
      loopRecording.length = 0;
      rewindTrail.length = 0;
      movePlayerToStart();
      updateRewindTrail();

      if (temporalStability <= 0) {
        temporalStability = 5;
        paradoxScore = Math.max(0, paradoxScore - 5);
        ghosts.length = 0;
        syncCooldowns.clear();
        setupNewCycle();
      }
    }
  }
}

function updateTimeLock(deltaTime) {
  const onAByPlayer = actorOnPad(player, padA);
  const onBByPlayer = actorOnPad(player, padB);

  let onAByGhost = false;
  let onBByGhost = false;
  for (const ghost of ghosts) {
    onAByGhost = onAByGhost || actorOnPad(ghost, padA);
    onBByGhost = onBByGhost || actorOnPad(ghost, padB);
  }

  const padAOccupied = onAByPlayer || onAByGhost;
  const padBOccupied = onBByPlayer || onBByGhost;
  const involvesGhost = onAByGhost || onBByGhost;
  const lockConditionMet = padAOccupied && padBOccupied && involvesGhost;

  if (!timeLock.unlocked && lockConditionMet) {
    timeLock.progress = clamp(timeLock.progress + deltaTime, 0, timeLock.unlockSeconds);
    if (timeLock.progress >= timeLock.unlockSeconds) {
      timeLock.unlocked = true;
    }
  } else if (!timeLock.unlocked) {
    timeLock.progress = clamp(timeLock.progress - deltaTime * 0.5, 0, timeLock.unlockSeconds);
  }
}

function updateSyncBonus(deltaTime) {
  for (let i = ghosts.length - 1; i >= 0; i -= 1) {
    const ghost = ghosts[i];
    const cooldown = syncCooldowns.get(ghost.id) || 0;

    if (cooldown > 0) {
      syncCooldowns.set(ghost.id, cooldown - deltaTime);
      continue;
    }

    if (distance(player, ghost) <= player.radius + ghost.radius) {
      paradoxScore += 1;
      syncCooldowns.set(ghost.id, 0.6);
    }
  }
}

function update(deltaTime) {
  let directionX = 0;
  let directionY = 0;
  const rewinding = keys.has('shift');

  if (keys.has('arrowleft') || keys.has('a')) directionX -= 1;
  if (keys.has('arrowright') || keys.has('d')) directionX += 1;
  if (keys.has('arrowup') || keys.has('w')) directionY -= 1;
  if (keys.has('arrowdown') || keys.has('s')) directionY += 1;

  if (!rewinding || !applyRewind()) {
    const length = Math.hypot(directionX, directionY) || 1;
    let movementSpeed = player.speed;
    if (isWaterTile(getTileTypeAt(player.x, player.y))) {
      movementSpeed *= 0.72;
    }

    const deltaX = (directionX / length) * movementSpeed * deltaTime;
    const deltaY = (directionY / length) * movementSpeed * deltaTime;

    const nextX = player.x + deltaX;
    const nextY = player.y + deltaY;
    if (canOccupyCircle(nextX, player.y, player.radius)) {
      player.x = nextX;
    }
    if (canOccupyCircle(player.x, nextY, player.radius)) {
      player.y = nextY;
    }

    updateRewindTrail();
    updateLoopRecording();
  }

  player.x = clamp(player.x, player.radius, canvas.width - player.radius);
  player.y = clamp(player.y, player.radius, canvas.height - player.radius);

  updateGhosts();
  updateEnemies(deltaTime);
  updateTimeLock(deltaTime);
  updateSyncBonus(deltaTime);

  if (timeLock.unlocked && !core.collected && distance(player, core) <= player.radius + core.radius) {
    paradoxScore += 5;
    core.collected = true;
    setupNewCycle();
  }
}

function drawGrid() {
  context.strokeStyle = 'rgba(255, 255, 255, 0.05)';
  context.lineWidth = 1;

  for (let x = 0; x < canvas.width; x += 48) {
    context.beginPath();
    context.moveTo(x, 0);
    context.lineTo(x, canvas.height);
    context.stroke();
  }

  for (let y = 0; y < canvas.height; y += 48) {
    context.beginPath();
    context.moveTo(0, y);
    context.lineTo(canvas.width, y);
    context.stroke();
  }
}

function drawTerrain() {
  for (let row = 0; row < MAP_ROWS; row += 1) {
    for (let col = 0; col < MAP_COLS; col += 1) {
      const tileType = terrainMap[row][col];

      if (tileType === 1) {
        context.fillStyle = '#1f3552';
      } else if (tileType === 2) {
        context.fillStyle = '#123e57';
      } else if (tileType === 3) {
        context.fillStyle = '#142f2d';
      } else {
        context.fillStyle = '#0f1a2e';
      }

      context.fillRect(col * TILE_SIZE, row * TILE_SIZE, TILE_SIZE, TILE_SIZE);
    }
  }
}

function drawEnemies() {
  for (const enemy of enemies) {
    context.fillStyle = enemy.hitCooldown > 0 ? '#f97363' : '#ff7f50';
    context.beginPath();
    context.arc(enemy.x, enemy.y, enemy.radius, 0, Math.PI * 2);
    context.fill();

    context.strokeStyle = 'rgba(255, 255, 255, 0.35)';
    context.lineWidth = 1.2;
    context.beginPath();
    context.arc(enemy.x, enemy.y, enemy.radius + 4, 0, Math.PI * 2);
    context.stroke();
  }
}

function drawPad(pad, active) {
  context.fillStyle = active ? 'rgba(107, 220, 255, 0.35)' : 'rgba(107, 220, 255, 0.14)';
  context.strokeStyle = active ? '#6bdcff' : 'rgba(107, 220, 255, 0.42)';
  context.lineWidth = 2;
  context.beginPath();
  context.arc(pad.x, pad.y, pad.radius, 0, Math.PI * 2);
  context.fill();
  context.stroke();
}

function drawCore() {
  if (core.collected) {
    return;
  }

  context.fillStyle = timeLock.unlocked ? '#ffd166' : 'rgba(255, 209, 102, 0.35)';
  context.beginPath();
  context.arc(core.x, core.y, core.radius, 0, Math.PI * 2);
  context.fill();
}

function render() {
  context.clearRect(0, 0, canvas.width, canvas.height);
  drawTerrain();
  drawGrid();

  const padAActive = actorOnPad(player, padA) || ghosts.some((ghost) => actorOnPad(ghost, padA));
  const padBActive = actorOnPad(player, padB) || ghosts.some((ghost) => actorOnPad(ghost, padB));
  drawPad(padA, padAActive);
  drawPad(padB, padBActive);
  drawCore();

  for (const ghost of ghosts) {
    context.fillStyle = 'rgba(255, 127, 80, 0.55)';
    context.beginPath();
    context.arc(ghost.x, ghost.y, ghost.radius, 0, Math.PI * 2);
    context.fill();
  }

  drawEnemies();

  context.fillStyle = '#6bdcff';
  context.beginPath();
  context.arc(player.x, player.y, player.radius, 0, Math.PI * 2);
  context.fill();

  context.fillStyle = '#e8eefc';
  context.font = '18px Segoe UI, sans-serif';
  context.fillText(`Paradox Score: ${paradoxScore}`, 20, 32);
  context.fillText(`Time Clones: ${ghosts.length}`, 20, 56);
  context.fillText(`Loops Created: ${loopsCreated}`, 20, 80);
  context.fillText(`Stability: ${temporalStability}/5`, 20, 104);
  context.fillText('Hold Shift to rewind | Press Space or T to create a time clone', 20, canvas.height - 20);

  const barX = canvas.width - 250;
  const barY = 24;
  const barWidth = 220;
  const barHeight = 16;
  const ratio = timeLock.progress / timeLock.unlockSeconds;

  context.strokeStyle = 'rgba(255, 255, 255, 0.35)';
  context.strokeRect(barX, barY, barWidth, barHeight);
  context.fillStyle = timeLock.unlocked ? 'rgba(255, 209, 102, 0.9)' : 'rgba(107, 220, 255, 0.8)';
  context.fillRect(barX, barY, barWidth * ratio, barHeight);

  context.fillStyle = '#e8eefc';
  context.fillText(timeLock.unlocked ? 'Lock: Open' : 'Lock: Charging', barX, barY + 34);
}

function loop(now) {
  const deltaTime = Math.min((now - lastTime) / 1000, 0.033);
  lastTime = now;

  update(deltaTime);
  render();
  requestAnimationFrame(loop);
}

fetch('/api/status')
  .then((response) => response.json())
  .then((data) => {
    serverStatus.textContent = data.message;
  })
  .catch(() => {
    serverStatus.textContent = 'Không kết nối được server local.';
  });

setupNewCycle();
movePlayerToStart();
updateRewindTrail();
requestAnimationFrame(loop);