package com.cloudpad.controller
import org.junit.Assert.*
import org.junit.Test
class ProtocolTest {
 @Test fun axisConversion(){assertEquals(Short.MIN_VALUE,axisShort(-1f));assertEquals(0,axisShort(0f).toInt());assertEquals(Short.MAX_VALUE,axisShort(1f))}
 @Test fun bitmask(){val mask=Buttons.A or Buttons.UP;assertTrue(mask and Buttons.A !=0);assertTrue(mask and Buttons.B ==0)}
 @Test fun packetLayout(){val bytes=packet(5,9,100,PadState(lx=.5f,rt=1f,buttons=Buttons.X),ByteArray(32){it.toByte()});assertEquals(61,bytes.size);assertEquals(1,bytes[0].toInt());assertEquals(255,bytes[26].toInt() and 255)}
 @Test fun deadzoneCalculation(){assertEquals(0f,deadzone(.1f,.15f));assertTrue(deadzone(.5f,.15f)>.4f)}
}
