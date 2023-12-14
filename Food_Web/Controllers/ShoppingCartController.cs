using Food_Web.Models;
using Food_Web.Other;
using Microsoft.Ajax.Utilities;
using Microsoft.AspNet.Identity;
using Newtonsoft.Json.Linq;
using PagedList;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using System.Web.UI;
using System.Xml.Schema;
using System.Configuration;
using static DevExpress.Xpo.Helpers.AssociatedCollectionCriteriaHelper;

namespace Food_Web.Models
{
    [Authorize]
    public class ShoppingCartController : Controller
    {
        FoodcontextDB db;
        public ShoppingCartController()
        {
            db = new FoodcontextDB();
        }

        public ActionResult Index(int? page)
        {
            //List<CartItem> ShoppingCart = GetCartItemsFromSession();
            var IDUser = User.Identity.GetUserId();
            var ShoppingCart = db.CartItems.Where(x => x.IdUser == IDUser).ToList();
            CheckAndUpdatePrices(ShoppingCart);
            int pageSize = 4; // số sản phẩm hiển thị trên mỗi trang
            int pageIndex = page.HasValue ? page.Value : 1; // trang hiện tại, nếu không có thì mặc định là 1
            IPagedList<CartItem> cartItems = ShoppingCart.ToPagedList(pageIndex, pageSize);
            CheckAndUpdatePrices(ShoppingCart);
            ViewBag.Tongsoluong = ShoppingCart.Sum(p => p.Quantity);
            ViewBag.Tongtien = ShoppingCart.Sum(p => p.Money);

            return View(cartItems);

        }

        private void CheckAndUpdatePrices(List<CartItem> shoppingCart)
        {
            foreach (var cartItem in shoppingCart)
            {
                var product = cartItem.Product;
                if (product != null)
                {
                    int updatedPrice = checkproduct(product);

                    // Check for discount expiration
                    if (product.Tinhtranggiamgia == true && product.DiscountStartTime.HasValue && DateTime.Now > product.DiscountEndTime)
                    {
                        // The discount has expired, update the price to the original price
                        updatedPrice = product.price.Value;
                    }

                    // Update the cart item with the new price
                    cartItem.Price = updatedPrice;
                    cartItem.Money = updatedPrice * cartItem.Quantity;

                    // Save changes to the database if needed
                    // db.SaveChanges();
                }
            }
        }

        [Authorize(Roles = "Admin")]
        public List<CartItem> GetCartItemsFromSession()
        {
            var lstShoppingCart = Session["ShoppingCart"] as List<CartItem>;
            if (lstShoppingCart == null)
            {
                lstShoppingCart = new List<CartItem>();
                Session["ShoppingCart"] = lstShoppingCart;

            }
            return lstShoppingCart;
        }
        //[Authorize]
        [HttpPost]
        public string AddToCart(int id)
        {

            try
            {

                var IDUser = User.Identity.GetUserId();
                var findCartItem = db.CartItems.FirstOrDefault(p => p.Productid == id && p.IdUser == IDUser);
                if (findCartItem == null)
                {
                    Product findsp = db.Products.First(m => m.Productid == id);

                    findCartItem = new CartItem();
                    findCartItem.IdUser = User.Identity.GetUserId();
                    findCartItem.Productid = findsp.Productid;
                    findCartItem.ProductName = findsp.Productname;
                    findCartItem.Quantity = 1;
                    findCartItem.Image = findsp.image;
                    findCartItem.Price = checkproduct(findsp);
                    db.CartItems.Add(findCartItem);
                }
                else
                {
                    findCartItem.Quantity++;
                }
                db.SaveChanges();
                return "Dat hang thanh cong!";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }


        public int checkproduct(Product findsp)
        {
            if (findsp != null)
            {
                if (findsp.Tinhtranggiamgia == true)
                {
                    return (int)findsp.GiaGiamTheoKhungGio;

                }
                else if (findsp.DiscountedPrice != null && findsp.price != null)
                {
                    return (int?)findsp.DiscountedPrice ?? findsp.price.Value;
                }
                else
                {
                    return findsp.price.Value;
                }
            }

            // Return a default value or handle the case where findsp is null
            return 0; // Default value (you can change it based on your requirements)
        }
        public RedirectToRouteResult UpdateCart(int id, int txtQuantity)
        {
            var itemFind = db.CartItems.FirstOrDefault(m => m.Id == id);
            if (itemFind != null)
            {
                itemFind.Quantity = txtQuantity;
            }
            return RedirectToAction("Index");
        }


        public ActionResult Delete()
        {
            using (var context = new FoodcontextDB())
            {
                var cartItems = context.CartItems.ToList();
                if (cartItems.Any())
                {
                    context.CartItems.RemoveRange(cartItems);
                    context.SaveChanges();
                }
                context.SaveChanges();
            }
            return RedirectToAction("Index");
        }


        public ActionResult RemoveCartItem(int Id)
        {
            using (var context = new FoodcontextDB())
            {
                // Lấy mục giỏ hàng cần xóa từ cơ sở dữ liệu
                var item = context.CartItems.FirstOrDefault(x => x.Id == Id);

                if (item != null)
                {
                    // Xóa mục giỏ hàng và lưu lại thay đổi vào cơ sở dữ liệu
                    context.CartItems.Remove(item);
                    context.SaveChanges();
                }
            }

            return RedirectToAction("Index");
        }


        //[HttpPost]
        //public JsonResult UpdateQuantity(int ID, int num)
        //{
        //    using (var context = new FoodcontextDB())
        //    {
        //        var cartItem = context.CartItems.FirstOrDefault(x => x.Id == ID);
        //        if (cartItem == null)
        //        {
        //            return Json(new { success = false });
        //        }

        //        var oldQuantity = cartItem.Quantity;
        //        cartItem.Quantity = num;
        //        context.SaveChanges();

        //        var updatedTotalPrice = cartItem.Price * cartItem.Quantity;

        //        var totalPrice = context.CartItems.Sum(x => x.Quantity * x.Price);
        //        var count = context.CartItems.Sum(x => x.Quantity);

        //        var updatedItem = new
        //        {
        //            ID = cartItem.Id,
        //            TotalPrice = updatedTotalPrice
        //        };

        //        return Json(new { success = true, item = updatedItem, totalPrice = totalPrice, count = count });
        //    }
        //}

        [HttpPost]
        public JsonResult UpdateQuantity(int ID, int num)
        {
            using (var context = new FoodcontextDB())
            {
                var cartItem = context.CartItems.Include(x => x.Product).FirstOrDefault(x => x.Id == ID);
                if (cartItem == null)
                {
                    return Json(new { success = false });
                }

                var oldQuantity = cartItem.Quantity;
                cartItem.Quantity = num;

                // Use the checkproduct method to get the updated price
                int updatedPrice = checkproduct(cartItem.Product);

                // Check for discount expiration
                if (cartItem.Product.Tinhtranggiamgia == true && cartItem.Product.DiscountStartTime.HasValue && DateTime.Now > cartItem.Product.DiscountEndTime)
                {
                    // The discount has expired, update the price to the original price
                    updatedPrice = cartItem.Product.price.Value;
                }

                cartItem.Price = updatedPrice;
                context.SaveChanges();

                var updatedTotalPrice = cartItem.Price * cartItem.Quantity;

                var totalPrice = context.CartItems.Sum(x => x.Quantity * x.Price);
                var count = context.CartItems.Sum(x => x.Quantity);

                var updatedItem = new
                {
                    ID = cartItem.Id,
                    TotalPrice = updatedTotalPrice
                };

                Session["updateprice"] = updatedTotalPrice;

                return Json(new { success = true, item = updatedItem, totalPrice = totalPrice, count = count });
            }
        }
        public ActionResult CartSummary()
        {
            int count = 0;
            var userId = User.Identity.GetUserId();
            var cartItems = db.CartItems.Where(h => h.IdUser == userId).ToList();
            if (cartItems != null)
            {
                count = cartItems.Count;
            }

            return Content(count.ToString());
        }



        public ActionResult Order()
        {

            string currentUserId = User.Identity.GetUserId();
            FoodcontextDB context = new FoodcontextDB();
            bool insufficientStock = false;
            List<Order_detail> orderDetails = new List<Order_detail>(); // Tạo danh sách để lưu thông tin chi tiết đơn hàng
            string voucherCode = Session["VoucherCode"] as string;
            double? totalpriceinvoucher = Session["TotalPriceAfterDiscount"] as double?;

            try
            {

                Order objOrder = new Order()
                {
                    Od_name = currentUserId,
                    Od_date = DateTime.Now,
                    Od_note = null,
                    Od_status = false,
                    Od_address = null,
                    VoidanOder = true,
                    idthanhtoan = 1

                };

                context.Orders.Add(objOrder);
                context.SaveChanges();

                int newOrderNo = objOrder.Od_id; // Giả sử bảng Order có cột "Id" đại diện cho số đơn hàng

                List<listOrder> listOrders = getListOrder();

                foreach (var order in listOrders)
                {
                    var cart = context.CartItems.SingleOrDefault(x => x.Id == order.ID);
                    if (cart != null)
                    {
                        var product = context.Products.FirstOrDefault(p => p.Productid == cart.Product.Productid);
                        if (product != null)
                        {
                            if (cart.Quantity > product.Soluong)
                            {
                                insufficientStock = true;
                                ViewBag.ErrorMessage = "Sản phẩm " + product.Productname + " không đủ hàng.";
                                // Redirect to some action to handle the error and display the message
                                return RedirectToAction("HandleError");
                            }
                            // Tạo chi tiết đơn hàng và lưu vào cơ sở dữ liệu
                            Order_detail ctdh = new Order_detail()
                            {
                                Od_id = newOrderNo,
                                Productid = cart.Product.Productid,
                                num = cart.Quantity,
                                tt_money = (double?)cart.Quantity * checkproduct(cart.Product),
                                price = checkproduct(cart.Product),
                                Storeid = cart.Product.Userid,
                                VoucherCode = voucherCode,
                                Totalinvoucher = totalpriceinvoucher
                            };
                            if (voucherCode != null && totalpriceinvoucher.HasValue && voucherCode == ctdh.VoucherCode && totalpriceinvoucher == ctdh.Totalinvoucher)
                            {
                                var discount = context.Discounts.SingleOrDefault(x => x.Code == voucherCode);
                                if (discount != null && discount.SoLuong > 0 || discount.Status == true)
                                {
                                    discount.SoLuong -= 1;
                                    UpdateDiscountStatus(voucherCode);
                                }
                                else
                                {
                                    string code = " Voucher không tồn tại hoặc hết hạn";
                                }
                            }
                            context.Order_detail.Add(ctdh);
                            orderDetails.Add(ctdh);

                            // Trừ đi số lượng đã mua từ sản phẩm
                            if (product != null)
                            {
                                product.Soluong -= cart.Quantity; // Giả sử Soluong là số lượng sản phẩm
                                context.SaveChanges(); // Lưu thay đổi số lượng vào cơ sở dữ liệu
                            }

                            context.CartItems.Remove(cart);
                            context.SaveChanges();

                        }
                    }
                }
                string subject = "Order Confirmation";
                string body = "Your order has been placed successfully.\n\n";
                body += "Order ID: " + newOrderNo + "\n";
                body += "Order Date: " + objOrder.Od_date + "\n";
                body += "Items:\n";
                foreach (var orderDetail in orderDetails)
                {
                    body += "\n  - Product ID: " + orderDetail.Productid + "\n";
                    body += "    Price: $" + orderDetail.price + "\n";
                    body += "    Quantity: " + orderDetail.num + "\n";
                }
                string toAddress = User.Identity.GetUserName(); // Get the user's email

                sendgmail(subject, body, toAddress);
            }
            catch (Exception ex)
            {
                //transaction.Rollback();
            }
            getListOrder().Clear();
            return RedirectToAction("Index");
        }
        public void UpdateDiscountStatus(string voucherCode)
        {
            using (var context = new FoodcontextDB())
            {
                var discount = context.Discounts.SingleOrDefault(x => x.Code == voucherCode);
                if (discount != null && discount.SoLuong > 0)
                {
                    discount.Status = true;
                    context.SaveChanges();
                }
                else
                {
                    discount.Status = false;
                    context.SaveChanges();
                }
            }
        }


        //public ActionResult Payment()
        //{

        //    FoodcontextDB context = new FoodcontextDB();
        //    string currentUserId = User.Identity.GetUserId();

        //    // Retrieve the maximum order ID and increment it by 1
        //    int newOrderNo = context.Orders.Max(o => (int?)o.Od_id) ?? 0;
        //    newOrderNo++;

        //    Order objOrder = new Order()
        //    {
        //        Od_name = currentUserId,
        //        Od_date = DateTime.Now,
        //        Od_note = null,
        //        Od_status = false,
        //        Od_address = null,
        //        VoidanOder = true,
        //        idthanhtoan = 1
        //    };

        //    context.Orders.Add(objOrder);

        //    string id = newOrderNo.ToString();


        //    //request params need to request to MoMo system
        //    string endpoint = "https://test-payment.momo.vn/gw_payment/transactionProcessor";
        //    string partnerCode = "MOMOOJOI20210710";
        //    string accessKey = "iPXneGmrJH0G8FOP";
        //    string serectkey = "sFcbSGRSJjwGxwhhcEktCHWYUuTuPNDB";
        //    string orderInfo = "test";
        //    string returnUrl = "https://localhost:44346/ShoppingCart/ConfirmPaymentClient";
        //    string notifyurl = "https://4c8d-2001-ee0-5045-50-58c1-b2ec-3123-740d.ap.ngrok.io/Home/SavePayment"; //lưu ý: notifyurl không được sử dụng localhost, có thể sử dụng ngrok để public localhost trong quá trình test

        //    string amount = "1000";
        //    //string orderid = DateTime.Now.Ticks.ToString(); //mã đơn hàng
        //    string orderid = " Mã Đơn Hàng :" + id; //mã đơn hàng
        //    string requestId = DateTime.Now.Ticks.ToString();
        //    string extraData = "";

        //    //Before sign HMAC SHA256 signature
        //    string rawHash = "partnerCode=" +
        //        partnerCode + "&accessKey=" +
        //        accessKey + "&requestId=" +
        //        requestId + "&amount=" +
        //        amount + "&orderId=" +
        //        orderid + "&orderInfo=" +
        //        orderInfo + "&returnUrl=" +
        //        returnUrl + "&notifyUrl=" +
        //        notifyurl + "&extraData=" +
        //        extraData;

        //    MoMoSecurity crypto = new MoMoSecurity();
        //    //sign signature SHA256
        //    string signature = crypto.signSHA256(rawHash, serectkey);

        //    //build body json request
        //    JObject message = new JObject
        //    {
        //        { "partnerCode", partnerCode },
        //        { "accessKey", accessKey },
        //        { "requestId", requestId },
        //        { "amount", amount },
        //        { "orderId", orderid },
        //        { "orderInfo", orderInfo },
        //        { "returnUrl", returnUrl },
        //        { "notifyUrl", notifyurl },
        //        { "extraData", extraData },
        //        { "requestType", "captureMoMoWallet" },
        //        { "signature", signature }

        //    };

        //    string responseFromMomo = PaymentRequest.sendPaymentRequest(endpoint, message.ToString());

        //    JObject jmessage = JObject.Parse(responseFromMomo);

        //    return Redirect(jmessage.GetValue("payUrl").ToString());
        //}
        public ActionResult Payment()
        {
            string currentUserId = User.Identity.GetUserId();
            FoodcontextDB context = new FoodcontextDB();
            bool insufficientStock = false;
            List<Order_detail> orderDetails = new List<Order_detail>(); // Tạo danh sách để lưu thông tin chi tiết đơn hàng
            string voucherCode = Session["VoucherCode"] as string;
            double? totalpriceinvoucher = Session["TotalPriceAfterDiscount"] as double?;
            double originalAmount = 0;


            int newOrderNo = context.Orders.Max(o => (int?)o.Od_id) ?? 0;
            newOrderNo++;

            Order objOrder = new Order()
            {
                Od_name = currentUserId,
                Od_date = DateTime.Now,
                Od_note = null,
                Od_status = false,
                Od_address = null,
                VoidanOder = true,
                idthanhtoan = 1
            };

            context.Orders.Add(objOrder);
            string oderid = newOrderNo.ToString();

            List<listOrder> listOrders = getListOrder();

            double? tt_money = 0;
            foreach (var order in listOrders)
            {
                var cart = context.CartItems.SingleOrDefault(x => x.Id == order.ID);
                if (cart != null)
                {
                    var product = context.Products.FirstOrDefault(p => p.Productid == cart.Product.Productid);
                    if (product != null)
                    {
                        if (cart.Quantity > product.Soluong)
                        {
                            insufficientStock = true;
                            ViewBag.ErrorMessage = "Sản phẩm " + product.Productname + " không đủ hàng.";
                            // Redirect to some action to handle the error and display the message
                            return RedirectToAction("HandleError");
                        }
                        // Tạo chi tiết đơn hàng và lưu vào cơ sở dữ liệu
                        Order_detail ctdh = new Order_detail()
                        {
                            Od_id = newOrderNo,
                            Productid = cart.Product.Productid,
                            num = cart.Quantity,
                            tt_money = (double?)cart.Quantity * checkproduct(cart.Product),
                            price = checkproduct(cart.Product),
                            Storeid = cart.Product.Userid,
                            VoucherCode = voucherCode,
                            Totalinvoucher = totalpriceinvoucher
                        };
                        if (voucherCode != null && totalpriceinvoucher.HasValue && voucherCode == ctdh.VoucherCode && totalpriceinvoucher == ctdh.Totalinvoucher)
                        {
                            var discount = context.Discounts.SingleOrDefault(x => x.Code == voucherCode);
                            if (discount != null && discount.SoLuong > 0 || discount.Status == true)
                            {
                                discount.SoLuong -= 1;
                                UpdateDiscountStatus(voucherCode);
                            }
                            else
                            {
                                string code = " Voucher không tồn tại hoặc hết hạn";
                            }
                        }
                        //context.Order_detail.Add(ctdh);
                        //orderDetails.Add(ctdh);



                        // Trừ đi số lượng đã mua từ sản phẩm
                        if (product != null)
                        {
                            product.Soluong -= cart.Quantity; // Giả sử Soluong là số lượng sản phẩm
                            //context.SaveChanges(); // Lưu thay đổi số lượng vào cơ sở dữ liệu
                        }

                        context.CartItems.Remove(cart);
                        // context.SaveChanges();

                    }
                }
            }


            string endpoint = "https://test-payment.momo.vn/gw_payment/transactionProcessor";
            string partnerCode = "MOMOOJOI20210710";
            string accessKey = "iPXneGmrJH0G8FOP";
            string serectkey = "sFcbSGRSJjwGxwhhcEktCHWYUuTuPNDB";
            string orderInfo = "test";
            string returnUrl = "https://localhost:44346/ShoppingCart/ConfirmPaymentClient";
            string notifyurl = "https://4c8d-2001-ee0-5045-50-58c1-b2ec-3123-740d.ap.ngrok.io/Home/SavePayment"; //lưu ý: notifyurl không được sử dụng localhost, có thể sử dụng ngrok để public localhost trong quá trình test

            //string amount = tt_money.ToString();
            //string orderid = DateTime.Now.Ticks.ToString(); //mã đơn hàng


            string amount = (tt_money + 1000).ToString();
            string orderid = newOrderNo+"";
            string requestId = DateTime.Now.Ticks.ToString();
            string extraData = "";

            //Before sign HMAC SHA256 signature
            string rawHash = "partnerCode=" +
                partnerCode + "&accessKey=" +
                accessKey + "&requestId=" +
                requestId + "&amount=" +
                amount + "&orderId=" +
                orderid + "&orderInfo=" +
                orderInfo + "&returnUrl=" +
                returnUrl + "&notifyUrl=" +
                notifyurl + "&extraData=" +
                extraData;

            MoMoSecurity crypto = new MoMoSecurity();
            //sign signature SHA256
            string signature = crypto.signSHA256(rawHash, serectkey);

            //build body json request
            JObject message = new JObject
            {
                { "partnerCode", partnerCode },
                { "accessKey", accessKey },
                { "requestId", requestId },
                { "amount", amount },
                { "orderId", orderid },
                { "orderInfo", orderInfo },
                { "returnUrl", returnUrl },
                { "notifyUrl", notifyurl },
                { "extraData", extraData },
                { "requestType", "captureMoMoWallet" },
                { "signature", signature }

            };

            string responseFromMomo = PaymentRequest.sendPaymentRequest(endpoint, message.ToString());

            JObject jmessage = JObject.Parse(responseFromMomo);

            return Redirect(jmessage.GetValue("payUrl").ToString());
        }

        //public ActionResult ConfirmPaymentClient(Result result)
        //{
        //    string rMessage = result.message;
        //    string rOrderId = result.orderId;
        //    string rErrorCode = result.errorCode;

        //    if (result != null && result.errorCode == "0")
        //    {
        //        string currentUserId = User.Identity.GetUserId(); // Lấy thông tin đăng nhập
        //        FoodcontextDB context = new FoodcontextDB();

        //        try
        //        {
        //            Order objOrder = new Order()
        //            {
        //                Od_name = currentUserId,
        //                Od_date = DateTime.Now,
        //                Od_note = null,
        //                Od_status = false,
        //                Od_address = null,
        //                idthanhtoan = 2
        //            };
        //            context.Orders.Add(objOrder);
        //            context.SaveChanges();

        //            int newOrderNo = objOrder.Od_id; // Giả sử bảng Order có cột "Id" đại diện cho số đơn hàng
        //            List<listOrder> listOrders = getListOrder();

        //            foreach (var item in listOrders)
        //            {
        //                var cart = context.CartItems.SingleOrDefault(x => x.Id == item.ID); // Sử dụng tham số 'id' truyền từ AJAX
        //                if (cart != null)
        //                {
        //                    Order_detail ctdh = new Order_detail()
        //                    {
        //                        Od_id = newOrderNo,
        //                        Productid = (int)cart.Productid,
        //                        tt_money = (double?)(cart.Quantity * (cart.Product.DiscountedPrice ?? cart.Product.price)),
        //                        price = cart.Product.DiscountedPrice ?? cart.Product.price,
        //                        Storeid = cart.Product.Userid,
        //                        num = cart.Quantity
        //                    };

        //                    context.CartItems.Remove(cart);
        //                    context.Order_detail.Add(ctdh);
        //                }
        //            }

        //            // Lưu thay đổi vào cơ sở dữ liệu ở cuối vòng lặp
        //            context.SaveChanges();

        //            string subject = "Order Confirmation";
        //            string body = "Your order has been placed successfully.\n\n";
        //            body += "Order ID: " + newOrderNo + "\n";
        //            body += "Order Date: " + objOrder.Od_date + "\n";
        //            body += "Items:\n";
        //            foreach (var orderDetail in context.Order_detail.Where(od => od.Od_id == newOrderNo))
        //            {
        //                body += "\n  - Product ID: " + orderDetail.Productid + "\n";
        //                body += "    Price: $" + orderDetail.price + "\n";
        //                body += "    Quantity: " + orderDetail.num + "\n";
        //                body += "    Tong thiet hai: " + orderDetail.tt_money + "\n";
        //            }
        //            string toAddress = User.Identity.GetUserName(); // Lấy email của người dùng

        //            sendgmail(subject, body, toAddress);
        //        }
        //        catch (Exception ex)
        //        {
        //            // Xử lý ngoại lệ tại đây
        //        }
        //    }

        //    return View();
        //}

        public ActionResult ConfirmPaymentClient(Result result)
        {
            string rMessage = result.message;
            string rOrderId = result.orderId;
            string rErrorCode = result.errorCode;

            if (result != null && result.errorCode == "0")
            {
                bool insufficientStock = false;
                List<Order_detail> orderDetails = new List<Order_detail>(); // Tạo danh sách để lưu thông tin chi tiết đơn hàng
                string voucherCode = Session["VoucherCode"] as string;
                double? totalpriceinvoucher = Session["TotalPriceAfterDiscount"] as double?;
                string currentUserId = User.Identity.GetUserId(); // Lấy thông tin đăng nhập
                FoodcontextDB context = new FoodcontextDB();

                try
                {

                    Order objOrder = new Order()
                    {
                        Od_name = currentUserId,
                        Od_date = DateTime.Now,
                        Od_note = null,
                        Od_status = false,
                        Od_address = null,
                        VoidanOder = true,
                        idthanhtoan = 2

                    };

                    //context.Orders.Add(objOrder);
                    //context.SaveChanges();

                    int newOrderNo = objOrder.Od_id; // Giả sử bảng Order có cột "Id" đại diện cho số đơn hàng

                    List<listOrder> listOrders = getListOrder();

                    foreach (var order in listOrders)
                    {
                        var cart = context.CartItems.SingleOrDefault(x => x.Id == order.ID);
                        if (cart != null)
                        {
                            var product = context.Products.FirstOrDefault(p => p.Productid == cart.Product.Productid);
                            if (product != null)
                            {
                                if (cart.Quantity > product.Soluong)
                                {
                                    insufficientStock = true;
                                    ViewBag.ErrorMessage = "Sản phẩm " + product.Productname + " không đủ hàng.";
                                    // Redirect to some action to handle the error and display the message
                                    return RedirectToAction("HandleError");
                                }
                                // Tạo chi tiết đơn hàng và lưu vào cơ sở dữ liệu
                                Order_detail ctdh = new Order_detail()
                                {
                                    Od_id = newOrderNo,
                                    Productid = cart.Product.Productid,
                                    num = cart.Quantity,
                                    tt_money = (double?)cart.Quantity * checkproduct(cart.Product),
                                    price = checkproduct(cart.Product),
                                    Storeid = cart.Product.Userid,
                                    VoucherCode = voucherCode,
                                    Totalinvoucher = totalpriceinvoucher
                                };
                                if (voucherCode != null && totalpriceinvoucher.HasValue && voucherCode == ctdh.VoucherCode && totalpriceinvoucher == ctdh.Totalinvoucher)
                                {
                                    var discount = context.Discounts.SingleOrDefault(x => x.Code == voucherCode);
                                    if (discount != null && discount.SoLuong > 0 || discount.Status == true)
                                    {
                                        discount.SoLuong -= 1;
                                        UpdateDiscountStatus(voucherCode);
                                    }
                                    else
                                    {
                                        string code = " Voucher không tồn tại hoặc hết hạn";
                                    }
                                }
                                context.Order_detail.Add(ctdh);
                                orderDetails.Add(ctdh);

                                // Trừ đi số lượng đã mua từ sản phẩm
                                if (product != null)
                                {
                                    product.Soluong -= cart.Quantity; // Giả sử Soluong là số lượng sản phẩm
                                    context.SaveChanges(); // Lưu thay đổi số lượng vào cơ sở dữ liệu
                                }

                                context.CartItems.Remove(cart);
                                context.SaveChanges();

                            }
                        }
                    }
                    string subject = "Order Confirmation";
                    string body = "Your order has been placed successfully.\n\n";
                    body += "Order ID: " + newOrderNo + "\n";
                    body += "Order Date: " + objOrder.Od_date + "\n";
                    body += "Items:\n";
                    foreach (var orderDetail in orderDetails)
                    {
                        body += "\n  - Product ID: " + orderDetail.Productid + "\n";
                        body += "    Price: $" + orderDetail.price + "\n";
                        body += "    Quantity: " + orderDetail.num + "\n";
                    }
                    string toAddress = User.Identity.GetUserName(); // Get the user's email

                    sendgmail(subject, body, toAddress);
                }
                catch (Exception ex)
                {
                    //transaction.Rollback();
                }
                getListOrder().Clear();
            }

            return View();
        }
        public List<listOrder> getListOrder()
        {
            var listOrders = Session["listOrder"] as List<listOrder>;
            if (listOrders == null)
            {
                listOrders = new List<listOrder>();
                Session["listOrder"] = listOrders;
            }
            return listOrders;
        }
        public JsonResult ClearSession()
        {
            getListOrder().Clear();
            return Json(new { success = true });
        }
        public JsonResult CheckOrder(int id)
        {
            //, int selectedPaymentMethodID
            FoodcontextDB context = new FoodcontextDB();
            List<listOrder> listOrders = getListOrder();
            var cart = context.CartItems.SingleOrDefault(x => x.Id == id);
            listOrder item = new listOrder()
            {
                ID = cart.Id,
                Name = cart.ProductName,
                Pic = cart.Image,
                gia = (decimal)cart.Product.price,
                Quantity = cart.Quantity.HasValue ? cart.Quantity.Value : 0

            };
            listOrders.Add(item);



            return Json(new { success = true });
        }


        public JsonResult CheckDiscout(List<int> listID, String code)
        {
            double totalPrice = 0;
            double salePrice = 0;
            double Price = 0;
            double precent = 0;

            if (listID == null) { return Json(new { success = false }); }
            if (code.Trim() == null) { return Json(new { success = false }); }
            FoodcontextDB context = new FoodcontextDB();
            var checkCode = context.Discounts.SingleOrDefault(x => x.Code == code);
            if (checkCode != null)
            {
                var currentDate = DateTime.Now;
                if (DateTime.Compare(currentDate, checkCode.StartDate) < 0)
                {
                    return Json(new { success = false });
                }
                if (DateTime.Compare(currentDate, (DateTime)checkCode.EndDate) > 0)
                {
                    return Json(new { success = false });
                }
                precent = checkCode.DiscountPercent;
                var storeID = checkCode.StoreId;
                Session["VoucherCode"] = code;
                foreach (var i in listID)
                {
                    var saleFood = context.CartItems.SingleOrDefault(x => x.Id == i && x.Product.Userid == storeID);
                    if (saleFood != null)
                    {
                        salePrice += (double)(saleFood.Price * saleFood.Quantity);
                    }
                    var food = context.CartItems.SingleOrDefault(x => x.Id == i && x.Product.Userid != storeID);
                    if (food != null)
                    {
                        Price += (double)(saleFood.Price * saleFood.Quantity);
                    }
                }
            }
            salePrice = salePrice * (100 - precent) / 100;
            totalPrice = salePrice + Price;
            Session["TotalPriceAfterDiscount"] = totalPrice;
            return Json(new { success = true, totalPrice = totalPrice });
        }
        [HttpPost]
        public ActionResult OrderItems()
        {
            List<listOrder> listOrders = getListOrder();
            //return View(listOrders);
            return PartialView("OrderItems", listOrders);

        }
        public void RemoveProcessedItemsFromSession(List<listOrder> processedItems)
        {
            List<listOrder> listOrders = Session["listOrder"] as List<listOrder>;
            if (listOrders != null)
            {
                foreach (var processedItem in processedItems)
                {
                    listOrders.Remove(processedItem);
                }
            }
        }

        public void sendgmail(string subject, string body, string toAddress)
        {
            try
            {
                var fromAddress = "sannguyen261102@gmail.com"; // Replace with your email address
                var fromPassword = "xkjfdjzcsswokxle"; // Replace with your email password

                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(fromAddress);
                    mail.To.Add(toAddress);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;

                    using (SmtpClient smtp = new SmtpClient("smtp.gmail.com"))
                    {
                        smtp.Port = 587;
                        smtp.UseDefaultCredentials = false;
                        smtp.Credentials = new NetworkCredential(fromAddress, fromPassword);
                        smtp.EnableSsl = true;

                        smtp.Send(mail);
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle the exception or log the error message for debugging
            }
        }


        public ActionResult PaymentVNPay()
        {
            string currentUserId = User.Identity.GetUserId();
            FoodcontextDB context = new FoodcontextDB();
            bool insufficientStock = false;
            List<Order_detail> orderDetails = new List<Order_detail>(); // Tạo danh sách để lưu thông tin chi tiết đơn hàng
            string voucherCode = Session["VoucherCode"] as string;
            double? totalpriceinvoucher = Session["TotalPriceAfterDiscount"] as double?;
            double originalAmount = 0;


            int newOrderNo = context.Orders.Max(o => (int?)o.Od_id) ?? 0;
            newOrderNo++;

            Order objOrder = new Order()
            {
                Od_name = currentUserId,
                Od_date = DateTime.Now,
                Od_note = null,
                Od_status = false,
                Od_address = null,
                VoidanOder = true,
                idthanhtoan = 1
            };

            context.Orders.Add(objOrder);
            string oderid = newOrderNo.ToString();

            List<listOrder> listOrders = getListOrder();

            foreach (var order in listOrders)
            {
                var cart = context.CartItems.SingleOrDefault(x => x.Id == order.ID);
                if (cart != null)
                {
                    var product = context.Products.FirstOrDefault(p => p.Productid == cart.Product.Productid);
                    if (product != null)
                    {
                        if (cart.Quantity > product.Soluong)
                        {
                            insufficientStock = true;
                            ViewBag.ErrorMessage = "Sản phẩm " + product.Productname + " không đủ hàng.";
                            // Redirect to some action to handle the error and display the message
                            return RedirectToAction("HandleError");
                        }
                        // Tạo chi tiết đơn hàng và lưu vào cơ sở dữ liệu
                        Order_detail ctdh = new Order_detail()
                        {
                            Od_id = newOrderNo,
                            Productid = cart.Product.Productid,
                            num = cart.Quantity,
                            tt_money = (double?)cart.Quantity * checkproduct(cart.Product),
                            price = checkproduct(cart.Product),
                            Storeid = cart.Product.Userid,
                            VoucherCode = voucherCode,
                            Totalinvoucher = totalpriceinvoucher
                        };
                        if (voucherCode != null && totalpriceinvoucher.HasValue && voucherCode == ctdh.VoucherCode && totalpriceinvoucher == ctdh.Totalinvoucher)
                        {
                            var discount = context.Discounts.SingleOrDefault(x => x.Code == voucherCode);
                            if (discount != null && discount.SoLuong > 0 || discount.Status == true)
                            {
                                discount.SoLuong -= 1;
                                UpdateDiscountStatus(voucherCode);
                            }
                            else
                            {
                                string code = " Voucher không tồn tại hoặc hết hạn";
                            }
                        }
                        //context.Order_detail.Add(ctdh);
                        //orderDetails.Add(ctdh);

                        double? tt_money = ctdh.tt_money;
                        originalAmount += tt_money ?? 0;


                        // Trừ đi số lượng đã mua từ sản phẩm
                        if (product != null)
                        {
                            product.Soluong -= cart.Quantity; // Giả sử Soluong là số lượng sản phẩm
                            //context.SaveChanges(); // Lưu thay đổi số lượng vào cơ sở dữ liệu
                        }

                        context.CartItems.Remove(cart);
                        // context.SaveChanges();

                    }
                }
            }
            string displayAmount = (originalAmount * 100).ToString();
            string url = ConfigurationManager.AppSettings["vnp_Url"];
            string returnUrl = ConfigurationManager.AppSettings["ReturnUrl"];
            string tmnCode = ConfigurationManager.AppSettings["vnp_TmnCode"];
            string hashSecret = ConfigurationManager.AppSettings["vnp_HashSecret"];

            PayLib pay = new PayLib();

            pay.AddRequestData("vnp_Version", "2.1.0"); //Phiên bản api mà merchant kết nối. Phiên bản hiện tại là 2.1.0
            pay.AddRequestData("vnp_Command", "pay"); //Mã API sử dụng, mã cho giao dịch thanh toán là 'pay'
            pay.AddRequestData("vnp_TmnCode", tmnCode); //Mã website của merchant trên hệ thống của VNPAY (khi đăng ký tài khoản sẽ có trong mail VNPAY gửi về)
            pay.AddRequestData("vnp_Amount", displayAmount); // Sử dụng trực tiếp giá trị `Gia`+//số tiền cần thanh toán, công thức: số tiền * 100 - ví dụ 10.000 (mười nghìn đồng) --> 1000000
            pay.AddRequestData("vnp_BankCode", ""); //Mã Ngân hàng thanh toán (tham khảo: https://sandbox.vnpayment.vn/apis/danh-sach-ngan-hang/), có thể để trống, người dùng có thể chọn trên cổng thanh toán VNPAY
            pay.AddRequestData("vnp_CreateDate", DateTime.Now.ToString("yyyyMMddHHmmss")); //ngày thanh toán theo định dạng yyyyMMddHHmmss
            pay.AddRequestData("vnp_CurrCode", "VND"); //Đơn vị tiền tệ sử dụng thanh toán. Hiện tại chỉ hỗ trợ VND
            pay.AddRequestData("vnp_IpAddr", Util.GetIpAddress()); //Địa chỉ IP của khách hàng thực hiện giao dịch
            pay.AddRequestData("vnp_Locale", "vn"); //Ngôn ngữ giao diện hiển thị - Tiếng Việt (vn), Tiếng Anh (en)
            pay.AddRequestData("vnp_OrderInfo", "Thanh toan don hang"); //Thông tin mô tả nội dung thanh toán
            pay.AddRequestData("vnp_OrderType", "other"); //topup: Nạp tiền điện thoại - billpayment: Thanh toán hóa đơn - fashion: Thời trang - other: Thanh toán trực tuyến
            pay.AddRequestData("vnp_ReturnUrl", returnUrl); //URL thông báo kết quả giao dịch khi Khách hàng kết thúc thanh toán
            pay.AddRequestData("vnp_TxnRef", oderid); //mã hóa đơn

            string paymentUrl = pay.CreateRequestUrl(url, hashSecret);

            db.SaveChanges();
            return Redirect(paymentUrl);
        }

        public ActionResult PaymentVNPayConfirm(Result result)
        {
            if (result != null && result.errorCode == "0")
            {
                try
                {
                    string currentUserId = User.Identity.GetUserId();
                    FoodcontextDB context = new FoodcontextDB();
                    string voucherCode = Session["VoucherCode"] as string;
                    double? totalpriceinvoucher = Session["TotalPriceAfterDiscount"] as double?;

                    Order objOrder = new Order()
                    {
                        Od_name = currentUserId,
                        Od_date = DateTime.Now,
                        Od_note = null,
                        Od_status = false,
                        Od_address = null,
                        VoidanOder = true,
                        idthanhtoan = 2
                    };

                    context.Orders.Add(objOrder);
                    context.SaveChanges();

                    int newOrderNo = objOrder.Od_id;

                    List<listOrder> listOrders = getListOrder();
                    List<Order_detail> orderDetails = new List<Order_detail>();
                    bool insufficientStock = false;

                    foreach (var order in listOrders)
                    {
                        var cart = context.CartItems.SingleOrDefault(x => x.Id == order.ID);
                        if (cart != null)
                        {
                            var product = context.Products.FirstOrDefault(p => p.Productid == cart.Product.Productid);
                            if (product != null)
                            {
                                if (cart.Quantity > product.Soluong)
                                {
                                    insufficientStock = true;
                                    ViewBag.ErrorMessage = $"Sản phẩm {product.Productname} không đủ hàng.";
                                    return RedirectToAction("HandleError");
                                }

                                Order_detail ctdh = new Order_detail()
                                {
                                    Od_id = newOrderNo,
                                    Productid = cart.Product.Productid,
                                    num = cart.Quantity,
                                    tt_money = (double?)cart.Quantity * checkproduct(cart.Product),
                                    price = checkproduct(cart.Product),
                                    Storeid = cart.Product.Userid,
                                    VoucherCode = Session["VoucherCode"] as string,
                                    Totalinvoucher = Session["TotalPriceAfterDiscount"] as double?
                                };

                                if (ctdh.VoucherCode != null && ctdh.Totalinvoucher.HasValue &&
                                    ctdh.VoucherCode == voucherCode && ctdh.Totalinvoucher == ctdh.Totalinvoucher)
                                {
                                    var discount = context.Discounts.SingleOrDefault(x => x.Code == voucherCode);
                                    if (discount != null && (discount.SoLuong > 0 || discount.Status == true))
                                    {
                                        discount.SoLuong -= 1;
                                        UpdateDiscountStatus(voucherCode);
                                    }
                                    else
                                    {
                                        ViewBag.ErrorMessage = "Voucher không tồn tại hoặc đã hết hạn";
                                        return RedirectToAction("HandleError");
                                    }
                                }

                                context.Order_detail.Add(ctdh);
                                orderDetails.Add(ctdh);

                                if (product != null)
                                {
                                    product.Soluong -= cart.Quantity;
                                    context.SaveChanges();
                                }

                                context.CartItems.Remove(cart);
                                context.SaveChanges();
                            }
                        }
                    }

                    string subject = "Order Confirmation";
                    string body = $"Your order has been placed successfully.\n\nOrder ID: {newOrderNo}\nOrder Date: {objOrder.Od_date}\nItems:\n";

                    foreach (var orderDetail in orderDetails)
                    {
                        body += $"\n  - Product ID: {orderDetail.Productid}\n    Price: ${orderDetail.price}\n    Quantity: {orderDetail.num}\n";
                    }

                    string toAddress = User.Identity.GetUserName();
                    sendgmail(subject, body, toAddress);
                }
                catch (Exception ex)
                {
                    ViewBag.ErrorMessage = $"Có lỗi xảy ra trong quá trình xử lý: {ex.Message}";
                    return RedirectToAction("HandleError");
                }

                return View();
            }
            else
            {
                ViewBag.Message = "Có lỗi xảy ra trong quá trình xử lý";
                return View();
            }
        }


    }

}       