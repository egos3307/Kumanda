package com.cloudpad.controller
import android.content.Context
import androidx.datastore.preferences.core.*
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.map
val Context.dataStore by preferencesDataStore("cloudpad")
data class PadSettings(val host:String="",val port:Int=26760,val leftDeadzone:Float=.12f,val rightDeadzone:Float=.12f,val sensitivity:Float=1f,val invertLeftY:Boolean=false,val invertRightY:Boolean=false,val haptic:Boolean=true,val rate:Int=60,val buttonSize:Float=1f,val stickSize:Float=1f,val opacity:Float=.85f,val autoReconnect:Boolean=true)
class SettingsStore(private val context:Context){private object K{val host=stringPreferencesKey("host");val port=intPreferencesKey("port");val ld=floatPreferencesKey("ld");val rd=floatPreferencesKey("rd");val sensitivity=floatPreferencesKey("sensitivity");val il=booleanPreferencesKey("il");val ir=booleanPreferencesKey("ir");val h=booleanPreferencesKey("haptic");val rate=intPreferencesKey("rate");val bs=floatPreferencesKey("button_size");val ss=floatPreferencesKey("stick_size");val opacity=floatPreferencesKey("opacity");val reconnect=booleanPreferencesKey("reconnect")}
 val flow=context.dataStore.data.map{p->PadSettings(p[K.host]?:"",p[K.port]?:26760,p[K.ld]?:.12f,p[K.rd]?:.12f,p[K.sensitivity]?:1f,p[K.il]?:false,p[K.ir]?:false,p[K.h]?:true,p[K.rate]?:60,p[K.bs]?:1f,p[K.ss]?:1f,p[K.opacity]?:.85f,p[K.reconnect]?:true)}
 suspend fun save(s:PadSettings)=context.dataStore.edit{p->p[K.host]=s.host;p[K.port]=s.port;p[K.ld]=s.leftDeadzone;p[K.rd]=s.rightDeadzone;p[K.sensitivity]=s.sensitivity;p[K.il]=s.invertLeftY;p[K.ir]=s.invertRightY;p[K.h]=s.haptic;p[K.rate]=s.rate;p[K.bs]=s.buttonSize;p[K.ss]=s.stickSize;p[K.opacity]=s.opacity;p[K.reconnect]=s.autoReconnect}
}
