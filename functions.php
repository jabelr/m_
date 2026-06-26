<?PHP
/**
 * @package   functions
 * @author    José Abel
 * @version   0.5
 * @date      2026-06-27
 */

  try {
    /**
     * Control de Hack
     */
    if (!defined('DONTHACK')) throw new Exception('DontHack');
  } catch (\Throwable $e) { die(header('HTTP/1.1 404 Error '.$e->getMessage())); }


  /**
   * Funcion de registro de logs
   */
  function logIt($msg):bool {
    try{
      file_put_contents($_SERVER['DOCUMENT_ROOT'].'/_logIt.txt',  date('Y-m-d H:i:s').' > '.debug_backtrace()[0]['file'].' >> '.$msg.PHP_EOL , FILE_APPEND | LOCK_EX);
    }catch (Throwable $t){ return false; }
    return true;
  }


  /**
   * Funcion de respuesta para Angular
   */
  function responseJSON(bool $isOK, $response, int $code = 0, int $totalRecords = 0):void {
    header('Content-Type: application/json; charset=utf-8');
    die(json_encode(array('isOK' => $isOK, 'response' => $response, 'code' => $code, 'totalrecords' => $totalRecords)));
  }


  /**
   * Formateo de nombre
   */
  function formatName(string $name):string {
    $name = str_replace("'", "`", $name);
    return mb_convert_case(mb_strtolower(trim($name)), MB_CASE_TITLE, "UTF-8");
  }


  /**
   * Formateo el nombre a mayuscula
   */
  function formatName2Upper(string $name):string {
    $name = str_replace("'", "`", $name);
    return mb_convert_case(mb_strtolower(trim($name)), MB_CASE_UPPER, "UTF-8");
  }


  /**
   * TimeSpan con milisegundos
   */
  function timeAgo(string $datetime):float {
    $date = new DateTime($datetime);
    return (float)($date->getTimestamp().'.'.$date->format('u'));
  }


  /**
   * Verificación del token
   */
  function checkToken():bool {
    global $secret_key;
    global $token;

    try{
      if (!preg_match("/^Bearer\s+(.*)$/", $_SERVER["HTTP_AUTHORIZATION"], $matches)) return false;

      if (preg_match("/^(?<header>.+)\.(?<payload>.+)\.(?<signature>.+)$/", $matches[1], $matches) !== 1) return false;

      $signature = hash_hmac("sha256", $matches["header"] . "." . $matches["payload"], $secret_key, true);
      $signature_from_token = base64URLDecode($matches["signature"]);

      if (!hash_equals($signature, $signature_from_token)) return false;

      $token = json_decode(base64URLDecode($matches["payload"]));
      $token->id = $token->id ?? null;
      if (!$token->id) return false;
      $token->id = decrypt($token->id);

    }catch (Throwable $t){
      logIt('errTokenSign >> '.$t->getMessage());
      responseJSON(false, 'errToken', ResponseJSONCode::ERR);
    }
    return true;
  }


  /**
   * En el caso de que quiera obtener el json enviado (payload)
   */
  function getJSON():mixed {
    $json = file_get_contents('php://input');
    if (!$json) responseJSON(false, 'notJSON', ResponseJSONCode::KO);

    return json_decode($json);
  }


  /**
   * Obtener la fecha de inicio de la campaña (1 de Octubre)
   */
  function getDateStart():string {
    if (date('m') < 10){
      return (date('Y') - 1).'-10-01 00:00:00';
    }else{
      return date('Y').'-10-01 00:00:00';
    }
  }


  /**
   * Redirect 301
   */
  function redirect301($url):void {
    global $app;
    $host = $app->host;
    header('HTTP/1.1 301 Moved Permanently');
    header('Location: '.$host.''.$url);
    die();
  }


  /**
   * Error 404
   */
  function err404($msg):void {
    header('HTTP/1.1 404 Error '.$msg);
    die();
  }


  /**
   * Validate date
   * @param $date Date to validate
   * @param $format Format of the date (default: Y-m-d H:i:s)
   * @return bool True if the date is valid, false otherwise
   */
  function validateDate($date, $format = 'Y-m-d H:i:s'):bool {
    $d = DateTime::createFromFormat($format, $date);
    return $d && $d->format($format) == $date;
  }


  /**
   * getIP
   */
  function getIP():string {
    if (isset($_SERVER['HTTP_X_FORWARDED_FOR'])) {
      $ip = $_SERVER['HTTP_X_FORWARDED_FOR'];
    }elseif (isset($_SERVER['HTTP_VIA'])) {
      $ip = $_SERVER['HTTP_VIA'];
    }elseif (isset($_SERVER['REMOTE_ADDR'])) {
      $ip = $_SERVER['REMOTE_ADDR'];
    }else{
      $ip = "Unknown";
    }
    return strip_tags($ip);
  }


  /**
   * Generate random word
   */
  function getRandomWord($len = 10):string {
    $word = array_merge(range('a', 'z'), range('A', 'Z'));
    shuffle($word);
    return substr(implode($word), 0, $len);
  }



  /**
   * Configure url for name
   */
  function prepareURL(string $name):string {
    $name = str_replace(" ", "_", $name);
    $name = preg_replace("/[^A-Za-z0-9_]/", '', strtoupper(trim($name)));
    return str_replace("__", "_", $name);
  }


  /**
   * Base64 URL Encode
   */
  function base64URLEncode(string $text):string {
    return str_replace(['+', '/', '='], ['-', '_', ''], base64_encode($text));
  }


  /**
   * Base64 URL Decode
   */
  function base64URLDecode(string $text):string {
    return base64_decode(str_replace(['-', '_'], ['+', '/'], $text));
  }


/**
 * Private Encrypt/Decrypt AES-256 function
 *
 * @param $action > Encrypt/Decrypt
 * @param $string String to encrypt/decrypt
 * @return string encrypted/decrypt
 */
function encrypt_decrypt($action, $string):string {
  $output = false;

  $encrypt_method = "AES-256-CBC";
  $secret_key = 'AS$%&/&Sdf//3··22¬@#~5%DFASdfa¬@#~5skSdfhgffa¬@#~5klj321';
  $secret_iv = '[ELEANTEC]/3·A·$·W$aff333smsl 4¬€·$%&/812999D++FAa331Sdfa¬@#~51dfSDF[ELEANTEC]';

  // hash
  $key = hash('sha256', $secret_key);

  // iv - encrypt method AES-256-CBC expects 16 bytes - else you will get a warning
  $iv = substr(hash('sha256', $secret_iv), 0, 16);

  if($action == 'encrypt' ) {
    $output = openssl_encrypt($string, $encrypt_method, $key, 0, $iv);
    $output = base64_encode($output);
  }elseif($action == 'decrypt' ){
    $output = openssl_decrypt(base64_decode($string), $encrypt_method, $key, 0, $iv);
  }

  return $output;
}

/**
 * Encrypt AES-256
 *
 * @param $string String to encrypt
 * @return string encrypted
 */
function encrypt($string):string {
  return encrypt_decrypt('encrypt', $string);
}

/**
 * Decrypt AES-256
 *
 * @param $string String to decrypt
 * @return string decrypted
 */
function decrypt($string):string {
  return encrypt_decrypt('decrypt', $string);
}