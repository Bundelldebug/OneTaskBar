using System;

namespace OneTaskBar
{
    internal sealed class TaskViewGesture
    {
        int win;
        bool tab;
        internal bool Observe(int key,bool down,int bufferedWin,bool enabled,bool modifiers,bool injected,out bool open)
        {
            open=false;if(injected)return false;
            if(!down&&key==win&&win!=0){win=0;return true;}
            if(key==9&&tab){if(!down)tab=false;return true;}
            if(!down||key!=9||bufferedWin==0||!enabled||modifiers)return false;
            win=bufferedWin;tab=true;open=true;return true;
        }
    }
}
