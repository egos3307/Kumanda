package com.cloudpad.controller
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
class MainActivity:ComponentActivity(){override fun onCreate(b:Bundle?){super.onCreate(b);setContent{MaterialTheme(colorScheme=darkColorScheme()){ConnectScreen()}}}
 @Composable fun ConnectScreen(){val store=remember{SettingsStore(this)};var saved by remember{mutableStateOf(PadSettings())};LaunchedEffect(Unit){store.flow.collect{saved=it}};var host by remember(saved.host){mutableStateOf(saved.host)};var port by remember(saved.port){mutableStateOf(saved.port.toString())};var pin by remember{mutableStateOf("")};var settingsOpen by remember{mutableStateOf(false)};val scope=rememberCoroutineScope()
  Surface(Modifier.fillMaxSize()){Column(Modifier.padding(24.dp).verticalScroll(rememberScrollState()),verticalArrangement=Arrangement.spacedBy(12.dp)){Text("CloudPad",style=MaterialTheme.typography.headlineLarge);OutlinedTextField(host,{host=it},label={Text("Server IP")},singleLine=true);OutlinedTextField(port,{port=it.filter(Char::isDigit)},label={Text("Port")},singleLine=true);OutlinedTextField(pin,{pin=it.filter(Char::isDigit).take(6)},label={Text("PIN / Pairing Code")},singleLine=true);Button(enabled=host.isNotBlank()&&pin.length==6,onClick={val p=port.toIntOrNull()?:26760;scope.launch{store.save(saved.copy(host=host,port=p))};startActivity(Intent(this@MainActivity,GamepadActivity::class.java).putExtra("host",host).putExtra("port",p).putExtra("pin",pin))}){Text("CONNECT")};OutlinedButton({settingsOpen=true}){Text("Settings")};Text("Status: Disconnected\nManual IP supports LAN and Tailscale 100.x.x.x addresses.")}}
  if(settingsOpen)SettingsDialog(saved,{saved=it;scope.launch{store.save(it)}},{settingsOpen=false})}
 }
@Composable fun SettingsDialog(initial:PadSettings,onSave:(PadSettings)->Unit,onClose:()->Unit){var s by remember{mutableStateOf(initial)};AlertDialog(onDismissRequest=onClose,title={Text("Gamepad Settings")},text={Column(Modifier.verticalScroll(rememberScrollState())){SliderSetting("Left deadzone",s.leftDeadzone,0f..0.4f){s=s.copy(leftDeadzone=it)};SliderSetting("Right deadzone",s.rightDeadzone,0f..0.4f){s=s.copy(rightDeadzone=it)};SliderSetting("Stick sensitivity",s.sensitivity,.5f..1.5f){s=s.copy(sensitivity=it)};SliderSetting("Button size",s.buttonSize,.7f..1.3f){s=s.copy(buttonSize=it)};SliderSetting("Joystick size",s.stickSize,.7f..1.3f){s=s.copy(stickSize=it)};SliderSetting("Opacity",s.opacity,.35f..1f){s=s.copy(opacity=it)};SwitchRow("Invert Left Y",s.invertLeftY){s=s.copy(invertLeftY=it)};SwitchRow("Invert Right Y",s.invertRightY){s=s.copy(invertRightY=it)};SwitchRow("Haptic feedback",s.haptic){s=s.copy(haptic=it)};SwitchRow("Auto reconnect",s.autoReconnect){s=s.copy(autoReconnect=it)};Text("Packet rate");Row{listOf(30,60,120).forEach{FilterChip(it==s.rate,{s=s.copy(rate=it)},{Text("$it Hz")},Modifier.padding(3.dp))}}}},confirmButton={Button({onSave(s);onClose()}){Text("Save")}},dismissButton={TextButton(onClose){Text("Cancel")}})}
@Composable fun SliderSetting(name:String,value:Float,range:ClosedFloatingPointRange<Float>,set:(Float)->Unit){Text("$name: ${"%.2f".format(value)}");Slider(value,set,valueRange=range)}
@Composable fun SwitchRow(name:String,value:Boolean,set:(Boolean)->Unit){Row(Modifier.fillMaxWidth(),horizontalArrangement=Arrangement.SpaceBetween){Text(name);Switch(value,set)}}
