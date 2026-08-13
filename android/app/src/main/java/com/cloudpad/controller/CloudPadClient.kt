package com.cloudpad.controller
import android.os.SystemClock
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableStateFlow
import org.json.JSONObject
import java.io.*
import java.net.*
import android.util.Base64
import java.util.concurrent.atomic.AtomicReference
enum class ConnectionStatus { DISCONNECTED,CONNECTING,CONNECTED,POOR_CONNECTION }
class CloudPadClient(private val scope:CoroutineScope){val status=MutableStateFlow(ConnectionStatus.DISCONNECTED);val ping=MutableStateFlow(0L);val state=AtomicReference(PadState());private var job:Job?=null
 fun connect(host:String,port:Int,pin:String,rate:Int,autoReconnect:Boolean){
  disconnect()
  job=scope.launch(Dispatchers.IO){
   var delayMs=1000L
   do {
    try { runSession(host,port,pin,rate); delayMs=1000L }
    catch(e:CancellationException){ throw e }
    catch(_:Exception){ status.value=ConnectionStatus.DISCONNECTED }
    if(!autoReconnect) break
    delay(delayMs)
    delayMs=(delayMs+1000L).coerceAtMost(5000L)
   } while(isActive)
  }
 }
 private suspend fun runSession(host:String,port:Int,pin:String,rate:Int)=coroutineScope{status.value=ConnectionStatus.CONNECTING;Socket().use{tcp->tcp.connect(InetSocketAddress(host,port),5000);tcp.tcpNoDelay=true;tcp.soTimeout=4000;val writer=BufferedWriter(OutputStreamWriter(tcp.getOutputStream()));val reader=BufferedReader(InputStreamReader(tcp.getInputStream()));writer.write(JSONObject().put("type","HELLO").put("protocolVersion",1).put("deviceName",android.os.Build.MODEL).put("pin",pin).toString());writer.newLine();writer.flush();val reply=JSONObject(reader.readLine()?:throw IOException("No reply"));if(reply.optString("type")!="PAIR_ACCEPTED")throw IOException(reply.optString("error","Pairing rejected"));val session=reply.getLong("sessionId").toInt();val token=Base64.decode(reply.getString("sessionToken"),Base64.DEFAULT);status.value=ConnectionStatus.CONNECTED
  val udp=DatagramSocket();var sequence=1;val endpoint=InetSocketAddress(host,port);val sender=launch{val interval=(1000L/rate.coerceIn(30,120));while(isActive){val bytes=packet(session,sequence++,System.currentTimeMillis(),state.get(),token);udp.send(DatagramPacket(bytes,bytes.size,endpoint));delay(interval)}}
  val heartbeat=launch{while(isActive){val t=SystemClock.elapsedRealtime();writer.write(JSONObject().put("type","PING").put("timestamp",t).toString());writer.newLine();writer.flush();val pong=withTimeout(3000){JSONObject(reader.readLine())};ping.value=SystemClock.elapsedRealtime()-pong.getLong("timestamp");status.value=if(ping.value>150)ConnectionStatus.POOR_CONNECTION else ConnectionStatus.CONNECTED;delay(1000)}}
  try{heartbeat.join()}finally{sender.cancelAndJoin();udp.close();state.set(PadState())}}}
 fun disconnect(){job?.cancel();job=null;state.set(PadState());status.value=ConnectionStatus.DISCONNECTED}
}
