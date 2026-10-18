using System;
using TMPro;
using UnityEngine;
//using static UnityEditor.U2D.ScriptablePacker;

public class ExamineUI : MonoBehaviour
{
    [SerializeField] private GameObject examineCanvas;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI contentText;

    private void Awake()
    {
        Close();
    }

    public void Open(ExamineData data)
    {
        examineCanvas.SetActive(true);

        // เอาข้อมูลไปใส่ UI
        titleText.text = data.title;
        contentText.text = data.content;
    }

    public void Close()
    {
        examineCanvas.SetActive(false);
    }
}

[Serializable]
public struct ExamineData
{
    public string title;
    [TextArea(3, 10)]
    public string content;
    public ExamineData(string title, string content)
    {
        this.title = title;
        this.content = content;
    }
}
