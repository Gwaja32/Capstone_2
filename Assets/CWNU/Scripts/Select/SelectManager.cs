using TMPro;
using UnityEngine;

public class SelectManager : MonoBehaviour
{
    [Header("My_Illust")]
    [SerializeField] private GameObject my_alix;
    [SerializeField] private GameObject my_echo;
    [SerializeField] private GameObject my_gorr;

    [Header("My_Name")]
    [SerializeField] private GameObject my_name;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        my_alix.SetActive(false);
        my_echo.SetActive(false);
        my_gorr.SetActive(false);
        my_name.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void HoverAlix()
    {
        my_echo.SetActive(false);
        my_gorr.SetActive(false);
        my_alix.SetActive(true);
        my_name.SetActive(true);
        my_name.GetComponent<TextMeshProUGUI>().text = "Alix";
    }

    public void HoverEcho()
    {
        my_gorr.SetActive(false);
        my_alix.SetActive(false);
        my_echo.SetActive(true);
        my_name.SetActive(true);
        my_name.GetComponent<TextMeshProUGUI>().text = "Echo";
    }

    public void HoverGorr()
    {
        my_echo.SetActive(false);
        my_alix.SetActive(false);
        my_gorr.SetActive(true);
        my_name.SetActive(true);
        my_name.GetComponent<TextMeshProUGUI>().text = "Gorr";
    }
}
