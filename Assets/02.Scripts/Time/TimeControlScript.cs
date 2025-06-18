using UnityEngine;

public class TimeControlScript : MonoBehaviour
{    public void TimeFast()
    {
        Time.timeScale = 30;
    }

    public void TimeNormal()
    {
        Time.timeScale = 1;
    }

    public void TimePause()
    {
        Time.timeScale = 0;
    }
}
