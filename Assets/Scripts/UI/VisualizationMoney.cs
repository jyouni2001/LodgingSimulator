using UnityEngine;
using TMPro;

public class VisualizationMoney : MonoBehaviour
{
    public TextMeshProUGUI moneyText;

    // Update is called once per frame
    void Update()
    {
        moneyText.text = "Money : " + PlayerWallet.Instance.Money.ToString();        
    }
}
