using System.Collections.Generic;
using UnityEngine;

public class SettingUI : MonoBehaviour
{
    [SerializeField] private GameObject m_SettingUI;

    [Header("Graphic")]
    [SerializeField] private GameObject m_VSyne;
    [SerializeField] private GameObject m_FrameRate;
    [SerializeField] private GameObject m_Resolution;
    [SerializeField] private GameObject m_Gamma;

    [Header("Sound")]
    [SerializeField] private GameObject m_BGM;
    [SerializeField] private GameObject m_SFX;

    [Header("Game")]
    [SerializeField] private GameObject m_Language;

    [SerializeField] private List<GameObject> m_UIObject;
    [SerializeField] private List<GameObject> m_GraphicUI;
    [SerializeField] private List<GameObject> m_SoundUI;

    private void Start()
    {
        m_SettingUI.SetActive(false);

        foreach (GameObject obj in m_UIObject)
        {
            obj.SetActive(false);
        }

        if (m_GraphicUI != null)
        {
            foreach (GameObject obj in m_GraphicUI)
            {
                obj.SetActive(true);
            }
        }
    }

    public void Btn_SettingUI()
    {
        if(m_SettingUI != null) m_SettingUI.SetActive(!m_SettingUI.activeSelf);
    }

    public void Btn_Graphic()
    {
        ResetUI();

        if (m_GraphicUI != null)
        {
            foreach (GameObject obj in m_GraphicUI)
            {
                obj.SetActive(true);
            }
        }
    }

    public void Btn_Sound()
    {
        ResetUI();

        if (m_SoundUI != null)
        {
            foreach (GameObject obj in m_SoundUI)
            {
                obj.SetActive(true);
            }
        }
    }

    public void Btn_GameSetting()
    {
        ResetUI();

        if (m_Language != null)
        {
            m_Language.SetActive(true);
        }
    }

    private void ResetUI()
    {
        foreach (GameObject obj in m_UIObject)
        {
            obj.SetActive(false);
        }
    }

}
