package com.cloudpad.controller
import java.nio.ByteBuffer
import java.nio.ByteOrder
import kotlin.math.abs
import kotlin.math.roundToInt

object Protocol { const val VERSION:Byte=1; const val PORT=26760; const val PACKET_SIZE=61; const val TOKEN_SIZE=32 }
object Buttons { const val A=1 shl 0;const val B=1 shl 1;const val X=1 shl 2;const val Y=1 shl 3;const val LB=1 shl 4;const val RB=1 shl 5;const val BACK=1 shl 6;const val START=1 shl 7;const val L3=1 shl 8;const val R3=1 shl 9;const val UP=1 shl 10;const val DOWN=1 shl 11;const val LEFT=1 shl 12;const val RIGHT=1 shl 13 }
data class PadState(val lx:Float=0f,val ly:Float=0f,val rx:Float=0f,val ry:Float=0f,val lt:Float=0f,val rt:Float=0f,val buttons:Int=0)
fun axisShort(v:Float):Short { val c=v.coerceIn(-1f,1f);return if(c<=-1f)Short.MIN_VALUE else (c*Short.MAX_VALUE).roundToInt().toShort() }
fun deadzone(v:Float,d:Float):Float { val a=abs(v);return if(a<=d)0f else kotlin.math.sign(v)*(a-d)/(1f-d) }
fun packet(session:Int,sequence:Int,time:Long,state:PadState,token:ByteArray):ByteArray { require(token.size==32);return ByteBuffer.allocate(Protocol.PACKET_SIZE).order(ByteOrder.LITTLE_ENDIAN).apply { put(Protocol.VERSION);putInt(session);putInt(sequence);putLong(time);putShort(axisShort(state.lx));putShort(axisShort(state.ly));putShort(axisShort(state.rx));putShort(axisShort(state.ry));put((state.lt.coerceIn(0f,1f)*255).roundToInt().toByte());put((state.rt.coerceIn(0f,1f)*255).roundToInt().toByte());putShort(state.buttons.toShort());put(token) }.array() }
