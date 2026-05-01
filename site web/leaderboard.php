<?php
header('Content-Type: application/json');
header('Access-Control-Allow-Origin: *');

define('DB_ACCESS', true);
require_once '../db.php';

// Meilleur temps par pilote × circuit × classe
$rows = $pdo->query('
    SELECT l.circuit, l.car_class, d.username, l.car_name,
           l.lap_time, l.sector1, l.sector2, l.sector3
    FROM laps l
    JOIN drivers d ON l.driver_id = d.id
    JOIN (
        SELECT driver_id, circuit, car_class, MIN(lap_time) AS best
        FROM laps GROUP BY driver_id, circuit, car_class
    ) b ON l.driver_id = b.driver_id
       AND l.circuit   = b.circuit
       AND l.car_class = b.car_class
       AND l.lap_time  = b.best
    ORDER BY l.circuit, l.car_class, l.lap_time ASC
')->fetchAll();

$data = [];
foreach ($rows as $r) {
    $data[$r['circuit']][$r['car_class']][] = [
        'username' => $r['username'],
        'carName'  => $r['car_name'],
        'lapTime'  => (float)$r['lap_time'],
        'sector1'  => $r['sector1'] !== null ? (float)$r['sector1'] : null,
        'sector2'  => $r['sector2'] !== null ? (float)$r['sector2'] : null,
        'sector3'  => $r['sector3'] !== null ? (float)$r['sector3'] : null,
    ];
}

echo json_encode($data);
