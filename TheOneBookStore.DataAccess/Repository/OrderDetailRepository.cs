using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheOneBookStore.DataAccess.Repository.IRepository;
using TheOneBookStore.Models;

namespace TheOneBookStore.DataAccess.Repository
{
    public class OrderDetailRepository : Repository<OrderDetail>, IOrderDetailRepository
	{
        private ApplicationDbContext _db;
        public OrderDetailRepository(ApplicationDbContext db) : base(db) 
        {
            _db = db;
        }

        public void Update(OrderDetail obj)
        {
            _db.OrderDetail.Update(obj);
        }
    }
}
