package com.cloudpad.controller
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.*
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.hapticfeedback.HapticFeedbackType
import androidx.compose.ui.input.pointer.*
import androidx.compose.ui.platform.LocalHapticFeedback
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.*
import kotlin.math.sqrt
class GamepadActivity:ComponentActivity(){private val scope=CoroutineScope(SupervisorJob()+Dispatchers.Main.immediate);private val client=CloudPadClient(scope)
 override fun onCreate(b:Bundle?){super.onCreate(b);window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);window.decorView.systemUiVisibility=5894;val host=intent.getStringExtra("host")?:return;val port=intent.getIntExtra("port",26760);val pin=intent.getStringExtra("pin")?:return;setContent{val settings by SettingsStore(this).flow.collectAsState(PadSettings());LaunchedEffect(host,port,pin,settings.rate){client.connect(host,port,pin,settings.rate,settings.autoReconnect)};MaterialTheme(colorScheme=darkColorScheme()){Gamepad(client,settings){finish()}}}}
 override fun onStop(){super.onStop();client.state.set(PadState())}override fun onDestroy(){client.disconnect();scope.cancel();super.onDestroy()}}
@Composable fun Gamepad(client:CloudPadClient,s:PadSettings,close:()->Unit){val status by client.status.collectAsState();val ping by client.ping.collectAsState();var pad by remember{mutableStateOf(PadState())};fun update(v:PadState){pad=v;client.state.set(v)}
 BoxWithConstraints(Modifier.fillMaxSize().background(Color(0xFF0C111A)).alpha(s.opacity)){Text("${status.name.replace('_',' ')}  •  ${ping}ms",Modifier.align(Alignment.TopCenter).padding(6.dp),color=Color.White)
  Row(Modifier.fillMaxSize().padding(12.dp),horizontalArrangement=Arrangement.SpaceBetween){Column(Modifier.fillMaxHeight(),verticalArrangement=Arrangement.SpaceBetween){Row{HoldButton("LT",s){update(pad.copy(lt=if(it)1f else 0f))};HoldButton("LB",s){update(pad.copy(buttons=pad.buttons.set(Buttons.LB,it)))}};Row(verticalAlignment=Alignment.CenterVertically){Joystick(s.stickSize){x,y->update(pad.copy(lx=deadzone(x*s.sensitivity,s.leftDeadzone),ly=deadzone((if(s.invertLeftY)-y else y)*s.sensitivity,s.leftDeadzone)))};Spacer(Modifier.width(10.dp));DPad(s){bit,on->update(pad.copy(buttons=pad.buttons.set(bit,on)))}}}
   Column(Modifier.fillMaxHeight(),horizontalAlignment=Alignment.CenterHorizontally,verticalArrangement=Arrangement.SpaceBetween){Row{HoldButton("BACK",s){update(pad.copy(buttons=pad.buttons.set(Buttons.BACK,it)))};Spacer(Modifier.width(12.dp));HoldButton("START",s){update(pad.copy(buttons=pad.buttons.set(Buttons.START,it)))}};OutlinedButton(close){Text("DISCONNECT")};Row{HoldButton("L3",s){update(pad.copy(buttons=pad.buttons.set(Buttons.L3,it)))};HoldButton("R3",s){update(pad.copy(buttons=pad.buttons.set(Buttons.R3,it)))}}}
   Column(Modifier.fillMaxHeight(),verticalArrangement=Arrangement.SpaceBetween,horizontalAlignment=Alignment.End){Row{HoldButton("RB",s){update(pad.copy(buttons=pad.buttons.set(Buttons.RB,it)))};HoldButton("RT",s){update(pad.copy(rt=if(it)1f else 0f))}};Row(verticalAlignment=Alignment.CenterVertically){Joystick(s.stickSize){x,y->update(pad.copy(rx=deadzone(x*s.sensitivity,s.rightDeadzone),ry=deadzone((if(s.invertRightY)-y else y)*s.sensitivity,s.rightDeadzone)))};Spacer(Modifier.width(10.dp));ABXY(s){bit,on->update(pad.copy(buttons=pad.buttons.set(bit,on)))}}}}
 }}
private fun Int.set(bit:Int,on:Boolean)=if(on)this or bit else this and bit.inv()
@Composable fun HoldButton(label:String,s:PadSettings,on:(Boolean)->Unit){val h=LocalHapticFeedback.current;Box(Modifier.padding(3.dp).size((48*s.buttonSize).dp).background(Color(0xFF344258),CircleShape).pointerInput(label,s.haptic){awaitEachGesture{val down=awaitFirstDown(requireUnconsumed=false);if(s.haptic)h.performHapticFeedback(HapticFeedbackType.TextHandleMove);on(true);do{val e=awaitPointerEvent()}while(e.changes.any{it.id==down.id&&it.pressed});on(false)}},contentAlignment=Alignment.Center){Text(label,color=Color.White)}}
@Composable fun Joystick(scale:Float,on:(Float,Float)->Unit){var knob by remember{mutableStateOf(Offset.Zero)};Box(Modifier.size((125*scale).dp).background(Color(0x553E5069),CircleShape).pointerInput(scale){awaitEachGesture{val down=awaitFirstDown();val id=down.id;var pressed=true;while(pressed){val e=awaitPointerEvent();val c=e.changes.firstOrNull{it.id==id};pressed=c?.pressed==true;if(c!=null){val center=Offset(size.width/2f,size.height/2f);var d=c.position-center;val radius=minOf(size.width,size.height)/2f;val length=sqrt(d.x*d.x+d.y*d.y);if(length>radius)d*=radius/length;knob=d;on(d.x/radius,d.y/radius);c.consume()}};knob=Offset.Zero;on(0f,0f)}}){Box(Modifier.offset{androidx.compose.ui.unit.IntOffset(knob.x.toInt(),knob.y.toInt())}.size((55*scale).dp).align(Alignment.Center).background(Color(0xFF6B7E98),CircleShape))}}
@Composable fun ABXY(s:PadSettings,change:(Int,Boolean)->Unit){Column(horizontalAlignment=Alignment.CenterHorizontally){HoldButton("Y",s){change(Buttons.Y,it)};Row{HoldButton("X",s){change(Buttons.X,it)};Spacer(Modifier.width(42.dp));HoldButton("B",s){change(Buttons.B,it)}};HoldButton("A",s){change(Buttons.A,it)}}}
@Composable fun DPad(s:PadSettings,change:(Int,Boolean)->Unit){Column(horizontalAlignment=Alignment.CenterHorizontally){HoldButton("↑",s){change(Buttons.UP,it)};Row{HoldButton("←",s){change(Buttons.LEFT,it)};HoldButton("→",s){change(Buttons.RIGHT,it)}};HoldButton("↓",s){change(Buttons.DOWN,it)}}}
