namespace JY.AI.Interfaces
{
    /// <summary>
    /// 결제 처리를 추상화하는 인터페이스
    /// AI 시스템이 PaymentSystem에 직접 의존하지 않도록 함
    /// </summary>
    public interface IPaymentProcessor
    {
        /// <summary>
        /// 결제 정보 추가
        /// </summary>
        void AddPayment(string aiId, int amount, string roomId, int reputation = 0);
        
        /// <summary>
        /// 결제 처리
        /// </summary>
        int ProcessPayment(string aiId);
        
        /// <summary>
        /// 미결제 금액 확인
        /// </summary>
        int GetPendingAmount(string aiId);
        
        /// <summary>
        /// 결제 완료 여부 확인
        /// </summary>
        bool IsPaymentCompleted(string aiId);
    }

    /// <summary>
    /// PaymentSystem을 IPaymentProcessor로 감싸는 어댑터
    /// </summary>
    public class PaymentSystemAdapter : IPaymentProcessor
    {
        private PaymentSystem paymentSystem;

        public PaymentSystemAdapter(PaymentSystem system)
        {
            paymentSystem = system;
        }

        public void AddPayment(string aiId, int amount, string roomId, int reputation = 0)
        {
            if (paymentSystem == null) return;

            if (reputation > 0)
            {
                paymentSystem.AddPayment(aiId, amount, roomId, reputation);
            }
            else
            {
                paymentSystem.AddPayment(aiId, amount, roomId);
            }
        }

        public int ProcessPayment(string aiId)
        {
            if (paymentSystem == null) return 0;
            return paymentSystem.ProcessPayment(aiId);
        }

        public int GetPendingAmount(string aiId)
        {
            if (paymentSystem?.paymentQueue == null) return 0;

            int totalAmount = 0;
            foreach (var payment in paymentSystem.paymentQueue)
            {
                if (payment.aiName == aiId && !payment.isPaid)
                {
                    totalAmount += payment.amount;
                }
            }
            return totalAmount;
        }

        public bool IsPaymentCompleted(string aiId)
        {
            return GetPendingAmount(aiId) == 0;
        }
    }
}