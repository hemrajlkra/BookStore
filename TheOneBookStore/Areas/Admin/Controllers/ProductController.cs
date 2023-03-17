using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TheOneBookStore.DataAccess;
using TheOneBookStore.DataAccess.Repository.IRepository;
using TheOneBookStore.Models;
using TheOneBookStore.Models.ViewModels;
using TheOneBookStore.Utility;

namespace TheOneBookStore.Areas.Admin.Controllers
{
    [Area("Admin")]
	[Authorize(Roles = SD.Role_Admin)]

	public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _hostEnvironment;
        public ProductController(IUnitOfWork unitOfWork, IWebHostEnvironment hostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _hostEnvironment = hostEnvironment;
        }
        public IActionResult Index()
        {
            IEnumerable<CoverType> objCoverTypeList = _unitOfWork.CoverType.GetAll();
            return View(objCoverTypeList);
        }
        

        public IActionResult Upsert(int? id)
        {
            ProductVM productVM = new()
            {
                Product = new(),
                CategoryList = _unitOfWork.Category.GetAll().Select(i=>new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                }),
                CoverTypeList = _unitOfWork.CoverType.GetAll().Select(i => new SelectListItem
                {
                    Text = i.Name,
                    Value = i.Id.ToString()
                })
            };
            if (id == null || id == 0)
            {
                //create product
                //ViewBag.CategoryList = CategoryList;
                //ViewData["CoverTypeList"] = CoverTypeList;
                return View(productVM);
            }
                
            else
            {
                //update product
                productVM.Product = _unitOfWork.Product.GetFirstOrDefault(i => i.Id == id);
                return View(productVM);
            }
            //var obj = _unitOfWork.Categories.Find(id);
            //var obj = _unitOfWork.CoverType.GetFirstOrDefault(u => u.Id == id);
            //if (obj == null)
            //    return NotFound();
            //return View(obj);
            
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(ProductVM obj, IFormFile? file)
        {
            if (ModelState.IsValid)
            {
                string wwwRootPath = _hostEnvironment.WebRootPath;
                if (file != null)
                {
                    string filename = Guid.NewGuid().ToString();
                    var uploads = Path.Combine(wwwRootPath, @"images\products");
                    var extension = Path.GetExtension(file.FileName);
                    if (obj.Product.ImageUrl != null)
                    {
                        var oldImagePath = Path.Combine(wwwRootPath, obj.Product.ImageUrl.TrimStart('\\'));
                        if (System.IO.File.Exists(oldImagePath))
                        {
                            System.IO.File.Delete(oldImagePath);
                        }
                    }
                    using (var fileStreams = new FileStream(Path.Combine(uploads, filename + extension), FileMode.Create))
                    {
                        file.CopyTo(fileStreams);
                    } 
                    obj.Product.ImageUrl = @"\images\products\" + filename+ extension;
                }
                if (obj.Product.Id == 0)
                {
                    _unitOfWork.Product.Add(obj.Product);
                    TempData["success"] = "Product created successfully";
                }
                else
                {
                    _unitOfWork.Product.Update(obj.Product);
                    TempData["success"] = "Product updated successfully";
                }
                
                _unitOfWork.Save();
                
                return RedirectToAction("Index");
            }
            return View(obj);
        }
        //public IActionResult Delete(int? id)
        //{
        //    if (id == null || id == 0) return NotFound();
        //    //var obj = _unitOfWork.Categories.Find(id);
        //    var obj = _unitOfWork.CoverType.GetFirstOrDefault(u => u.Id == id);
        //    if (obj == null)
        //        return BadRequest();
        //    return View(obj);
        //}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePost(int? id)
        {
            //var obj = _unitOfWork.Find(id);
            var obj = _unitOfWork.CoverType.GetFirstOrDefault(u => u.Id == id);
            if (obj == null)
                return NotFound();
            _unitOfWork.CoverType.Remove(obj);
            _unitOfWork.Save(); 
            TempData["success"] = "CoverType Deleted successfully";
            return RedirectToAction("Index");
        }
        #region Api Calls
        [HttpGet]
        public IActionResult GetAll() 
        {
            var prodList = _unitOfWork.Product.GetAll(includeProperties: "Category,CoverType");
            return Json(new { data = prodList });

        }
        #endregion

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            //var obj = _unitOfWork.Find(id);
            var obj = _unitOfWork.Product.GetFirstOrDefault(u => u.Id == id);
            if (obj == null)
                return Json(new {success=false,message="Error while deleting!!"});
            var oldImagePath = Path.Combine(_hostEnvironment.WebRootPath, obj.ImageUrl.TrimStart('\\'));
            if (System.IO.File.Exists(oldImagePath))
            {
                System.IO.File.Delete(oldImagePath);
            }
            _unitOfWork.Product.Remove(obj);
            _unitOfWork.Save();

            return Json(new { success = true, message = "Deleted successfully" });
        }

    }
}
