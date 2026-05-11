<?php
header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');

define('DB_ACCESS', true);
require_once '../db.php';

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    http_response_code(405); die(json_encode(['error' => 'Method not allowed']));
}

$body = json_decode(file_get_contents('php://input'), true);
if (!$body) {
    http_response_code(400); die(json_encode(['error' => 'Invalid JSON']));
}

foreach (['username','token','circuit','carClass','lapTime'] as $f) {
    if (empty($body[$f])) {
        http_response_code(400); die(json_encode(['error' => "Missing: $f"]));
    }
}

$username = mb_substr(trim($body['username']), 0, 50);
$token    = mb_substr(trim($body['token']),    0, 64);
$circuit  = mb_substr(trim($body['circuit']),  0, 100);
$carClass = mb_substr(trim($body['carClass']), 0, 30);
$carName  = mb_substr(trim($body['carName'] ?? ''), 0, 100);
$lapTime  = (float)$body['lapTime'];
$s1       = isset($body['sector1']) ? (float)$body['sector1'] : null;
$s2       = isset($body['sector2']) ? (float)$body['sector2'] : null;
$s3       = isset($body['sector3']) ? (float)$body['sector3'] : null;
$version  = mb_substr(trim($body['version'] ?? ''), 0, 20);

if ($lapTime <= 0 || $lapTime > 86400) {
    http_response_code(400); die(json_encode(['error' => 'Invalid lap time']));
}

// Créer ou retrouver le pilote
$stmt = $pdo->prepare('SELECT id, token FROM drivers WHERE username = ?');
$stmt->execute([$username]);
$driver = $stmt->fetch();

if (!$driver) {
    $pdo->prepare('INSERT INTO drivers (username, token) VALUES (?, ?)')->execute([$username, $token]);
    $driverId = $pdo->lastInsertId();
} else {
    if ($driver['token'] !== $token) {
        http_response_code(403); die(json_encode(['error' => 'Invalid token']));
    }
    $driverId = $driver['id'];
}

$pdo->prepare('
    INSERT INTO laps (driver_id, circuit, car_class, car_name, lap_time, sector1, sector2, sector3, app_version)
    VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
')->execute([$driverId, $circuit, $carClass, $carName, $lapTime, $s1, $s2, $s3, $version]);

echo json_encode(['success' => true]);
