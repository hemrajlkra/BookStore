using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Stripe.Checkout;
using System.Diagnostics;
using System.Security.Claims;
using TheOneBookStore.DataAccess.Repository.IRepository;
using TheOneBookStore.Models;
using TheOneBookStore.Models.ViewModels;
using TheOneBookStore.Utility;

namespace TheOneBookStore.Areas.Admin.Controllers
{
	[Area("Admin")]
	[Authorize]
	public class OrderController : Controller
	{
		private readonly IUnitOfWork _unitOfWork;
		[BindProperty]
		public OrderVM OrderVM { get; set; }
		public OrderController(IUnitOfWork unitOfWork)
		{
			_unitOfWork = unitOfWork;
		}
		public IActionResult Index()
		{
			return View();
		}
		public IActionResult Details(int orderId)
		{
			OrderVM = new OrderVM()
			{
				orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == orderId, includeProperties: "ApplicationUser"),
				orderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderId == orderId, includeProperties: "Product")
			};
			return View(OrderVM);
		}
		[ActionName("Details")]
		[HttpPost]
		[ValidateAntiForgeryToken]
		public IActionResult Details_PAY_NOW(int orderId)
		{
			OrderVM.orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.orderHeader.Id, includeProperties: "ApplicationUser");
			OrderVM.orderDetail = _unitOfWork.OrderDetail.GetAll(u => u.OrderId == OrderVM.orderHeader.Id, includeProperties: "Product");

			var domainUrl = "https://localhost:44385/";
			var options = new SessionCreateOptions
			{
				PaymentMethodTypes = new List<string>
					{
						"card"
					},
				LineItems = new List<SessionLineItemOptions>(),
				Mode = "payment",
				SuccessUrl = domainUrl + $"admin/order/PaymentConfirmation?orderHeaderid={OrderVM.orderHeader.Id}" ,
				CancelUrl = domainUrl + $"admin/order/Details?orderHeaderid={OrderVM.orderHeader.Id}",
			};
			foreach (var item in OrderVM.orderDetail)
			{
				var sessionLineItem = new SessionLineItemOptions
				{
					PriceData = new SessionLineItemPriceDataOptions
					{
						UnitAmount = (long)(item.Price * 100),
						Currency = "usd",
						ProductData = new SessionLineItemPriceDataProductDataOptions
						{
							Name = item.Product.Title,
						},
					},
					Quantity = item.Count,
				};
				options.LineItems.Add(sessionLineItem);
			}
			var service = new SessionService();
			Session session = service.Create(options);
			_unitOfWork.OrderHeader.UpdateStripePaymentId(OrderVM.orderHeader.Id, session.Id, session.PaymentIntentId);
			_unitOfWork.Save();
			Response.Headers.Add("Location", session.Url);
			return new StatusCodeResult(303);
		}

		public IActionResult PaymentConfirmation(int orderHeaderid)
		{
			OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == orderHeaderid);
			if (orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
			{
				var service = new SessionService();
				Session session = service.Get(orderHeader.SessionId);
				//orderHeader.PaymentIntentId = session.PaymentIntentId;
				//checking status if paid?
				if (session.PaymentStatus.ToLower() == "paid")
				{
					
					_unitOfWork.OrderHeader.UpdateStatus(orderHeaderid, orderHeader.OrderStatus, SD.PaymentStatusApproved);
					_unitOfWork.Save();
				}
			}
			return View(orderHeaderid);

		}
		[HttpPost]
		[Authorize(Roles =SD.Role_Admin+","+SD.Role_Employee)]
		[ValidateAntiForgeryToken]
		public IActionResult UpdateOrderDetail()
		{
			var orderHeaderFromDb = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.orderHeader.Id, tracked:false);
			orderHeaderFromDb.Name = OrderVM.orderHeader.Name;
			orderHeaderFromDb.PhoneNumber = OrderVM.orderHeader.PhoneNumber;
			orderHeaderFromDb.StreetAddress = OrderVM.orderHeader.StreetAddress;
			orderHeaderFromDb.City = OrderVM.orderHeader.City;
			orderHeaderFromDb.State = OrderVM.orderHeader.State;
			orderHeaderFromDb.PostalCode = OrderVM.orderHeader.PostalCode;
			if (OrderVM.orderHeader.Carrier != null)
			{
				orderHeaderFromDb.Carrier= OrderVM.orderHeader.Carrier;
			}
			if (OrderVM.orderHeader.TrackingNumber != null)
			{
				orderHeaderFromDb.TrackingNumber = OrderVM.orderHeader.TrackingNumber;
			}
			_unitOfWork.OrderHeader.Update(orderHeaderFromDb);
			_unitOfWork.Save();
			TempData["success"] = "Order details updated successfully!!";
			return RedirectToAction("Details", "Order", new { orderId = orderHeaderFromDb.Id });

		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[ValidateAntiForgeryToken]
		public IActionResult StartProcessing()
		{
			_unitOfWork.OrderHeader.UpdateStatus(OrderVM.orderHeader.Id,SD.StatusInProcess);
			_unitOfWork.Save();
			TempData["success"] = "Order status updated successfully!!";
			return RedirectToAction("Details", "Order", new { orderId = OrderVM.orderHeader.Id });
		}
		[HttpPost]
		[Authorize(Roles =SD.Role_Admin+","+SD.Role_Employee)]
		[ValidateAntiForgeryToken]
		public IActionResult ShipOrder()
		{
			var orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.orderHeader.Id, tracked: false);
			orderHeader.TrackingNumber= OrderVM.orderHeader.TrackingNumber;
			orderHeader.Carrier = OrderVM.orderHeader.Carrier;
			orderHeader.OrderStatus = SD.StatusShipped;
			orderHeader.ShippingDate = DateTime.Now;
			if(orderHeader.PaymentStatus == SD.PaymentStatusDelayedPayment)
				orderHeader.PaymentDueDate = DateTime.Now.AddDays(30);
			_unitOfWork.OrderHeader.Update(orderHeader);
			_unitOfWork.Save();
			TempData["success"] = "Order shipped successfully!!";
			return RedirectToAction("Details", "Order", new { orderId = OrderVM.orderHeader.Id });
		}

		[HttpPost]
		[Authorize(Roles = SD.Role_Admin + "," + SD.Role_Employee)]
		[ValidateAntiForgeryToken]
		public IActionResult CancelOrder()
		{
			var orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == OrderVM.orderHeader.Id, tracked: false);
			if(orderHeader.PaymentStatus == SD.PaymentStatusApproved)
			{
				var options = new RefundCreateOptions
				{
					Reason = RefundReasons.RequestedByCustomer,
					PaymentIntent = orderHeader.PaymentIntentId
				};
				var service = new RefundService();
				Refund refund = service.Create(options);
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusRefunded);
			}
			else
			{
				_unitOfWork.OrderHeader.UpdateStatus(orderHeader.Id, SD.StatusCancelled, SD.StatusCancelled);
			}


			_unitOfWork.Save();
			TempData["success"] = "Order cancelled successfully!!";
			return RedirectToAction("Details", "Order", new { orderId = OrderVM.orderHeader.Id });
		}
		#region Api Calls
		[HttpGet]
		public IActionResult GetAll(string status)
		{
			IEnumerable<OrderHeader> orderHeaders;
			if(User.IsInRole(SD.Role_Admin) || User.IsInRole(SD.Role_Employee)) 
			{
				orderHeaders = _unitOfWork.OrderHeader.GetAll(includeProperties: "ApplicationUser");
            }
			else
			{
				var claimsIdentity = (ClaimsIdentity)User.Identity;
				var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                orderHeaders = _unitOfWork.OrderHeader.GetAll(u=>u.ApplicationUserId==claim.Value,includeProperties: "ApplicationUser");

            }
            switch (status)
			{
                case "pending":
					orderHeaders = orderHeaders.Where(u => u.PaymentStatus == SD.PaymentStatusDelayedPayment);
                    break;
				case "inprocess":
                    orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusInProcess);
                    break;
                case "completed":
                    orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusShipped);
                    break;
                case "approved":
                    orderHeaders = orderHeaders.Where(u => u.OrderStatus == SD.StatusApproved);
                    break;
                default:
                    break;
            }
			return Json(new { data = orderHeaders });
		}
		#endregion
	}
}
