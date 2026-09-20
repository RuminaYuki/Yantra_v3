public interface ICutsceneListener
{
    // API สำหรับรับคำสั่งเมื่อคัทซีนเริ่มหรือจบ
    void OnCutsceneStateChanged(bool isPlaying);
}