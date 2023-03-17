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

	public class CompanyController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        
        public CompanyController(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            
        }
        public IActionResult Index()
        {
            return View();
        }
        

        public IActionResult Upsert(int? id)
        {
            Company company = new();
            if (id == null || id == 0)
            {
                return View(company);
            } 
            else
            {
                company = _unitOfWork.Company.GetFirstOrDefault(i => i.Id == id);
                return View(company);
            }    
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Upsert(Company obj)
        {
            if (ModelState.IsValid)
            {
                if (obj.Id == 0)
                {
                    _unitOfWork.Company.Add(obj);
                    TempData["success"] = "Company created successfully!!";
                }
                else
                {
                    _unitOfWork.Company.Update(obj);
                    TempData["success"] = "Company updated successfully!!";
                }
                _unitOfWork.Save();
                return RedirectToAction("Index");
            }
            return View(obj);
        }
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public IActionResult DeletePost(int? id)
        //{
        //    var obj = _unitOfWork.Company.GetFirstOrDefault(u => u.Id == id);
        //    if (obj == null)
        //        return NotFound();
        //    _unitOfWork.Company.Remove(obj);
        //    _unitOfWork.Save(); 
        //    TempData["success"] = "Deleted successfully";
        //    return RedirectToAction("Index");
        //}
        #region Api Calls
        [HttpGet]
        public IActionResult GetAll() 
        {
            var companyList = _unitOfWork.Company.GetAll();
            return Json(new { data = companyList });

        }
        

        [HttpDelete]
        public IActionResult Delete(int? id)
        {
            var obj = _unitOfWork.Company.GetFirstOrDefault(u => u.Id == id);
            if (obj == null)
                return Json(new {success=false,message="Error while deleting!!"});
            _unitOfWork.Company.Remove(obj);
            _unitOfWork.Save();

            return Json(new { success = true, message = "Deleted successfully" });
        }
        #endregion

    }
}
