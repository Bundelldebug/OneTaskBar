using System;
using System.IO;
using System.Windows.Media;
namespace OneTaskBar
{
    internal sealed class ChargeSound : IDisposable
    {
        readonly ChargeState connection=new ChargeState();MediaPlayer player;
        internal void Observe(bool known,bool connected)
        {
            if(!known||!connection.Observe(connected)||!Preferences.Current.ChargeSoundEnabled)return;
            Play(Preferences.Current.ChargeSoundFile,null);
        }
        internal static bool Supported(string path){if(String.IsNullOrWhiteSpace(path))return false;try{string type=Path.GetExtension(path);return Array.Exists(new[]{".wav",".mp3",".wma",".m4a",".aac"},extension=>String.Equals(extension,type,StringComparison.OrdinalIgnoreCase));}catch(ArgumentException){return false;}}
        internal bool Play(string path,Action<string> failed)
        {
            Dispose();if(!Supported(path)||!File.Exists(path)){if(failed!=null)failed("소리 파일을 찾을 수 없습니다. 파일을 다시 선택해 주세요.");return false;}
            try{var audio=new MediaPlayer {Volume=1};player=audio;audio.MediaEnded+=(s,e)=>Release(audio);audio.MediaFailed+=(s,e)=> {Release(audio);AppLog.Once("charge-sound",e.ErrorException.GetType().Name);if(failed!=null)failed("선택한 소리 파일을 재생하지 못했습니다. WAV 또는 MP3 파일을 선택해 주세요.");};audio.Open(new Uri(Path.GetFullPath(path)));audio.Play();return true;}
            catch(Exception error){Dispose();AppLog.Once("charge-sound",error.GetType().Name);if(failed!=null)failed("소리 파일을 재생하지 못했습니다.");return false;}
        }
        void Release(MediaPlayer audio){audio.Close();if(Object.ReferenceEquals(player,audio))player=null;}
        public void Dispose(){if(player!=null){player.Close();player=null;}}
    }
}
