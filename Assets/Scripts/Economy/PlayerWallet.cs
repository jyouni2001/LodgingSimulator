using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }
    
    [SerializeField] private int money = 1000;

    public int Money => money;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddMoney(int amount)
    {
        money += amount;
    }

    public void SpendMoney(int amount)
    {
        if (money >= amount)
        {
            money -= amount;
        }
    }
}