using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheOneBookStore.DataAccess.Repository.IRepository;
using TheOneBookStore.Models;

namespace TheOneBookStore.DataAccess.Repository
{
    public class OrderHeaderRepository : Repository<OrderHeader>, IOrderHeaderRepository    
	{
        private ApplicationDbContext _db;
        public OrderHeaderRepository(ApplicationDbContext db) : base(db) 
        {
            _db = db;
        }

        public void Update(OrderHeader obj)
        {
            _db.OrderHeader.Update(obj);
        }

		public void UpdateStatus(int id, string orderStatus, string? paymentStatus = null)
		{
			var orderFromDb = _db.OrderHeader.FirstOrDefault(x => x.Id == id);
            if(orderFromDb != null)
            {
                orderFromDb.OrderStatus = orderStatus;
                if(paymentStatus != null)
                {
                    orderFromDb.PaymentStatus = paymentStatus;
                }
            }
		}
		public void UpdateStripePaymentId(int id, string sessionId, string paymentItentId)
		{
			var orderFromDb = _db.OrderHeader.FirstOrDefault(x => x.Id == id);
            orderFromDb.PaymentDate = DateTime.Now;
			orderFromDb.SessionId = sessionId;
            orderFromDb.PaymentIntentId = paymentItentId;
		}
	}
}
