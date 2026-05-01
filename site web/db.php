<?php
if (!defined('DB_ACCESS')) die('Accès interdit');

$host = 'sql.free.fr';      // hôte MySQL free.fr
$dbname = 'TON_LOGIN';      // sur free.fr : nom de BDD = ton login
$user   = 'TON_LOGIN';
$pass   = 'TON_MOT_DE_PASSE';

try {
    $pdo = new PDO("mysql:host=$host;dbname=$dbname;charset=utf8mb4", $user, $pass, [
        PDO::ATTR_ERRMODE            => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
    ]);
} catch (PDOException $e) {
    http_response_code(500);
    die(json_encode(['error' => 'DB unavailable']));
}
