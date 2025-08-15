using UnityEngine;

public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; set; }
    
    [Header("돈 설정")]
    [SerializeField] private int _money = 1000;
    
    [Header("디버그 설정")]
    [SerializeField] private bool showMoneyLogs = true;
    
    // 공개 읽기 전용 프로퍼티
    public int money 
    { 
        get => _money; 
        private set 
        { 
            _money = Mathf.Max(0, value); // 음수 방지
        } 
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// 돈 추가 (수입)
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0)
        {
            if (showMoneyLogs)
                Debug.LogWarning($"[PlayerWallet] 잘못된 금액: {amount}");
            return;
        }
        
        int prevMoney = money;
        money += amount;
        
        if (showMoneyLogs)
            Debug.Log($"[PlayerWallet] 수입: +{amount} (이전: {prevMoney} → 현재: {money})");
    }

    /// <summary>
    /// 돈 사용 (지출) - 성공 여부 반환
    /// </summary>
    public bool SpendMoney(int amount)
    {
        if (amount <= 0)
        {
            if (showMoneyLogs)
                Debug.LogWarning($"[PlayerWallet] 잘못된 지출 금액: {amount}");
            return false;
        }
        
        if (money < amount)
        {
            if (showMoneyLogs)
                Debug.LogWarning($"[PlayerWallet] 잔액 부족: 필요 {amount}, 보유 {money}");
            return false;
        }
        
        int prevMoney = money;
        money -= amount;
        
        if (showMoneyLogs)
            Debug.Log($"[PlayerWallet] 지출: -{amount} (이전: {prevMoney} → 현재: {money})");
        
        return true;
    }
    
    /// <summary>
    /// 잔액 확인
    /// </summary>
    public bool CanAfford(int amount)
    {
        return money >= amount && amount > 0;
    }
    
    /// <summary>
    /// 돈 직접 설정 (세이브/로드, 디버그용)
    /// </summary>
    public void SetMoney(int amount)
    {
        int prevMoney = money;
        money = amount;
        
        if (showMoneyLogs)
            Debug.Log($"[PlayerWallet] 금액 설정: {prevMoney} → {money}");
    }
}