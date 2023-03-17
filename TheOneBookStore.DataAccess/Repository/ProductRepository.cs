using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheOneBookStore.DataAccess.Repository.IRepository;
using TheOneBookStore.Models;

namespace TheOneBookStore.DataAccess.Repository
{
    public class ProductRepository : Repository<Product>, IProductRepository  
    {
        private ApplicationDbContext _db;
        public ProductRepository(ApplicationDbContext db) : base(db) 
        {
            _db = db;
        }

        public void Update(Product obj)
        {
            var objFromDb = _db.Products.FirstOrDefault(p => p.Id == obj.Id);
            if(objFromDb != null)
            {
                objFromDb.Title= obj.Title;
                objFromDb.ISBN= obj.ISBN;
                objFromDb.Description= obj.Description;
                objFromDb.Price= obj.Price;
                objFromDb.Price50= obj.Price50;
                objFromDb.Price100= obj.Price100;
                objFromDb.ListPrice= obj.ListPrice;
                objFromDb.CategoryId= obj.CategoryId;
                objFromDb.CoverTypeId= obj.CoverTypeId;
                objFromDb.Author= obj.Author;
                if(objFromDb.ImageUrl!=null)
                {
                    objFromDb.ImageUrl= obj.ImageUrl;
                }

            }
        }
    }
}
