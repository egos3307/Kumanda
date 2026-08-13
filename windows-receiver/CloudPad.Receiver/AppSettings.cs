using System.Text.Json;
using System.IO;
using Microsoft.Win32;
namespace CloudPad.Receiver;
public sealed class AppSettings
{
 public int Port{get;set;}=26760; public int PacketTimeoutMs{get;set;}=500; public bool AutoStartServer{get;set;}=true;
 public bool StartMinimized{get;set;} public bool EnableLogging{get;set;}=true; public bool StartWithWindows{get;set;}
 static string Dir=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CloudPad");
 public static AppSettings Load(){try{return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path.Combine(Dir,"settings.json")))??new();}catch{return new();}}
 public void Save(){Directory.CreateDirectory(Dir);File.WriteAllText(Path.Combine(Dir,"settings.json"),JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
  using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true);
  if(StartWithWindows)key?.SetValue("CloudPad",$"\"{Environment.ProcessPath}\" --minimized");else key?.DeleteValue("CloudPad",false);}
 public static void Log(string level,string message,bool enabled=true){if(!enabled)return;try{Directory.CreateDirectory(Dir);File.AppendAllText(Path.Combine(Dir,"receiver.log"),$"{DateTimeOffset.Now:u} [{level}] {message}{Environment.NewLine}");}catch{}}
}
