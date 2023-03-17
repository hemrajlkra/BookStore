using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
using TheOneBookStore.DataAccess.Repository.IRepository;
using TheOneBookStore.Models;
using TheOneBookStore.Models.ViewModels;
using TheOneBookStore.Utility;

namespace TheOneBookStore.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]
    public class CartController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IEmailSender _emailSender;
        //[BindProperty]
        public ShoppingCartVM ShoppingCartVM { get; set; }
        public int orderTotal { get; set; }
        public CartController(IUnitOfWork unitOfWork,IEmailSender emailSender)
        {
            _unitOfWork=unitOfWork;
            _emailSender=emailSender;
        }
        public IActionResult Index()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            ShoppingCartVM = new ShoppingCartVM()
            {
                ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value,
                includeProperties:"Product"),
                OrderHeaders = new()
            };
            foreach(var cart in ShoppingCartVM.ListCart)
            {
                cart.Price = GetPriceBasedOnQuantity(cart.Count, cart.Product.Price,
                    cart.Product.Price50, cart.Product.Price100);
				//ShoppingCartVM.CartTotal+= (cart.Price * cart.Count);
				ShoppingCartVM.OrderHeaders.OrderTotal += (cart.Price * cart.Count);

			}
			return View(ShoppingCartVM);
        }
		public IActionResult Summary()
		{
            var claimsIdentity = (ClaimsIdentity)User.Identity;
            var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            ShoppingCartVM = new ShoppingCartVM()
            {
                ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value,
                includeProperties: "Product"),
                OrderHeaders = new()
            };
            ShoppingCartVM.OrderHeaders.ApplicationUser = _unitOfWork.ApplicationUser.GetFirstOrDefault(
                u => u.Id == claim.Value);
            ShoppingCartVM.OrderHeaders.Name = ShoppingCartVM.OrderHeaders.ApplicationUser.Name;
			ShoppingCartVM.OrderHeaders.PhoneNumber = ShoppingCartVM.OrderHeaders.ApplicationUser.PhoneNumber;
			ShoppingCartVM.OrderHeaders.StreetAddress = ShoppingCartVM.OrderHeaders.ApplicationUser.StreetAddress;
			ShoppingCartVM.OrderHeaders.City = ShoppingCartVM.OrderHeaders.ApplicationUser.City;
			ShoppingCartVM.OrderHeaders.State = ShoppingCartVM.OrderHeaders.ApplicationUser.State;
			ShoppingCartVM.OrderHeaders.PostalCode = ShoppingCartVM.OrderHeaders.ApplicationUser.PostalCode;

			foreach (var cart in ShoppingCartVM.ListCart)
            {
                cart.Price = GetPriceBasedOnQuantity(cart.Count, cart.Product.Price,
                    cart.Product.Price50, cart.Product.Price100);
                ShoppingCartVM.OrderHeaders.OrderTotal += (cart.Price * cart.Count);
            }
            return View(ShoppingCartVM);
            
		}

        [HttpPost]
        [ActionName("Summary")]
        [ValidateAntiForgeryToken]
		public IActionResult SummaryPost(ShoppingCartVM ShoppingCartVM)
		{
			var claimsIdentity = (ClaimsIdentity)User.Identity;
			var claim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
            //[bindproperties] or pass paramater in method-> SummaryPost(ShoppingCartVM ShoppingCartVM)
            ShoppingCartVM.ListCart = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == claim.Value,
                includeProperties: "Product");
            
			ShoppingCartVM.OrderHeaders.OrderDate = System.DateTime.Now;
			ShoppingCartVM.OrderHeaders.ApplicationUserId = claim.Value;

			foreach (var cart in ShoppingCartVM.ListCart)
			{
				cart.Price = GetPriceBasedOnQuantity(cart.Count, cart.Product.Price,
					cart.Product.Price50, cart.Product.Price100);
				ShoppingCartVM.OrderHeaders.OrderTotal += (cart.Price * cart.Count);
			}

            ApplicationUser applicationUser = _unitOfWork.ApplicationUser.GetFirstOrDefault(u=>u.Id== claim.Value);
            if (applicationUser.CompanyId.GetValueOrDefault() == 0)
            {
				ShoppingCartVM.OrderHeaders.PaymentStatus = SD.PaymentStatusPending;
				ShoppingCartVM.OrderHeaders.OrderStatus = SD.StatusPending;
			}
            else
            {
				ShoppingCartVM.OrderHeaders.PaymentStatus = SD.PaymentStatusDelayedPayment;
				ShoppingCartVM.OrderHeaders.OrderStatus = SD.StatusApproved;
			}


			_unitOfWork.OrderHeader.Add(ShoppingCartVM.OrderHeaders);
			_unitOfWork.Save();


			foreach (var cart in ShoppingCartVM.ListCart)
			{
                OrderDetail orderDetail = new() {
                    ProductId = cart.ProductId,
                    OrderId = ShoppingCartVM.OrderHeaders.Id,
                    Price = cart.Price,
                    Count= cart.Count
                };
                _unitOfWork.OrderDetail.Add(orderDetail);
                _unitOfWork.Save();
			}

            //stripe Settings
            if(applicationUser.CompanyId.GetValueOrDefault()==0)
            {

            
                var domainUrl = "https://localhost:44385/";
                var options = new SessionCreateOptions
                {
				    PaymentMethodTypes = new List<string>
                    {
                        "card"
                    },
                    LineItems = new List<SessionLineItemOptions>(),
                    Mode = "payment",
                    SuccessUrl =  domainUrl+ $"customer/cart/OrderConfirmation?id="+Uri.EscapeUriString($"{ShoppingCartVM.OrderHeaders.Id}"),
                    CancelUrl = domainUrl+$"customer/cart/Index",
                };
			    foreach (var item in ShoppingCartVM.ListCart)
                {
                    var sessionLineItem = new SessionLineItemOptions
                    {
                        PriceData = new SessionLineItemPriceDataOptions
                        {
                            UnitAmount = (long)(item.Price*100),
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
                _unitOfWork.OrderHeader.UpdateStripePaymentId(ShoppingCartVM.OrderHeaders.Id, session.Id, session.PaymentIntentId);
                _unitOfWork.Save();
                Response.Headers.Add("Location", session.Url);
                return new StatusCodeResult(303);

				//_unitOfWork.ShoppingCart.RemoveRange(ShoppingCartVM.ListCart);
				//_unitOfWork.Save();

				//return RedirectToAction("Index", "Home");
			}
            else
            {
                return RedirectToAction("OrderConfirmation", "cart", new { id= ShoppingCartVM.OrderHeaders.Id });
            }
		}
        public IActionResult OrderConfirmation(int id)
		{
            OrderHeader orderHeader = _unitOfWork.OrderHeader.GetFirstOrDefault(u => u.Id == id,includeProperties:"ApplicationUser");
            if(orderHeader.PaymentStatus != SD.PaymentStatusDelayedPayment)
            {
			    var service = new SessionService();
			    Session session = service.Get(orderHeader.SessionId);
                //orderHeader.PaymentIntentId = session.PaymentIntentId;
                //checking status if paid?
                if (session.PaymentStatus.ToLower() == "paid")
                {
					_unitOfWork.OrderHeader.UpdateStripePaymentId(id, orderHeader.SessionId, session.PaymentIntentId);
					_unitOfWork.OrderHeader.UpdateStatus(id, SD.StatusApproved, SD.PaymentStatusApproved);
                    _unitOfWork.Save();
                }

                //_unitOfWork.Save();
            }
            _emailSender.SendEmailAsync(orderHeader.ApplicationUser.Email, "TheOneBookStore",
                "<h2>Thankyou for shopping with us!!</h2><br/><p>Your order has been Created.</p>");
			List<ShoppingCart> shoppingCarts = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId ==
            orderHeader.ApplicationUserId).ToList();
            HttpContext.Session.Clear();
            _unitOfWork.ShoppingCart.RemoveRange(shoppingCarts);
            _unitOfWork.Save();
            return View(id);

        }
        public IActionResult Plus(int cartId)
        {
            var cart = _unitOfWork.ShoppingCart.GetFirstOrDefault(x => x.Id == cartId);
            _unitOfWork.ShoppingCart.IncrementCount(cart, 1);
            _unitOfWork.Save();
            return RedirectToAction(nameof(Index));
        }
		public IActionResult minus(int cartId)
		{
			var cart = _unitOfWork.ShoppingCart.GetFirstOrDefault(x => x.Id == cartId);
            if (cart.Count <= 1)
            {
				_unitOfWork.ShoppingCart.Remove(cart);
                var count = _unitOfWork.ShoppingCart.GetAll(u => u.ApplicationUserId == cart.ApplicationUserId).ToList().Count-1;
                HttpContext.Session.SetInt32(SD.SessionCart, count);
            }
            else
            {
			    _unitOfWork.ShoppingCart.DecrementCount(cart, 1);   
            }
			_unitOfWork.Save();
			return RedirectToAction(nameof(Index));
		}
		public IActionResult remove(int cartId)
		{
			var cart = _unitOfWork.ShoppingCart.GetFirstOrDefault(x => x.Id == cartId);

            _unitOfWork.ShoppingCart.Remove(cart);
			_unitOfWork.Save();
            var count = _unitOfWork.ShoppingCart.GetAll(u=>u.ApplicationUserId==cart.ApplicationUserId).ToList().Count;
            HttpContext.Session.SetInt32(SD.SessionCart,count);
			return RedirectToAction(nameof(Index));
		}
		public double GetPriceBasedOnQuantity(double quantity, double price,double price50, double price100)
        {
            if (quantity <= 50)
                return price;
            else
            {
                if (quantity <= 100)
                    return price50;
                return price100;
            }
        }
    }
}
