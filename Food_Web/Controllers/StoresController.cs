using Food_Web.Models;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Net;
using System.Threading.Tasks;
using System.Data.Entity;
using PagedList;
using System.IO;
using Microsoft.AspNet.SignalR;


namespace Food_Web.Models
{
    public class StoresController : Controller
    {

        //public ActionResult Index(string storeid)
        //{
        //    var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext()));
        //    var userList = userManager.Users.Where(u => u.IsApproved).ToList();
        //    return View(userList);
        //}
        public ActionResult Index(string storeid)
        {
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(new ApplicationDbContext()));

            // Get the current time
            var currentTime = DateTime.Now.TimeOfDay;

            // Get the list of users filtered by the storeid and opening hours
            var userList = userManager.Users.Where(u => u.IsApproved).ToList();

            foreach (var user in userList)
            {
                // Compare the current time with the opening and closing hours
                if (currentTime < user.Opentime || currentTime > user.Closetime)
                {
                    user.status = "Closed";
                    ViewBag.Status = user.status;
                }
                else
                {
                    user.status = "Open";
                    ViewBag.Status = user.status;
                }
            }

            // Store the open or closed status in the ViewBag
            

            return View(userList);
        }
        private FoodcontextDB db = new FoodcontextDB();
        public ActionResult Details(string id)
        {
             
                 int productId;
            if (!int.TryParse(id, out productId))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            Product product = db.Products.Find(productId);
            if (product == null)
            {
                return HttpNotFound();
            }

            // Continue with your code logic to display the product details

            return View(product);
        }


        public static string getIdStore = "";

            public ActionResult StoreProducts(string id)
            {
            Session["CurrentStoreId"] = id;
            getIdStore = id;
                if (string.IsNullOrEmpty(id))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
                }

                using (var db = new ApplicationDbContext())
                {
                    var products = db.Products.Where(p => p.Userid == id /*&& p.status == true*/).ToList();
                    ViewBag.userId = id;

                    return View(products);
                }
            }

        //[HttpPost]
        // [ValidateAntiForgeryToken]
        // public async Task<ActionResult> Create([Bind(Include = "content, created_at, Rating, storeId")] Comment comment , string storeId)
        // {
        //     getIdStore= storeId;
        //     if (!User.Identity.IsAuthenticated)
        //     {
        //         return RedirectToAction("Login", "Account");
        //     }
        //     if (ModelState.IsValid)
        //     {
        //         using (var db = new FoodcontextDB())
        //         {
        //             // Retrieve the maximum comment_id from the database
        //             int maxCommentId = db.Comments.Max(c => c.comment_id);

        //             // Increment the comment_id by 1 to generate a new unique ID
        //             comment.comment_id = maxCommentId + 1;

        //             // Set the user_id and Store_id
        //             comment.user_id = User.Identity.GetUserId();
        //             comment.Store_id = storeId;
        //             comment.created = DateTime.Now;

        //             db.Comments.Add(comment);
        //             await db.SaveChangesAsync();
        //         }

        //         return RedirectToAction("StoreProducts", new { id = storeId });
        //     }

        //     return View("StoreProducts");
        // }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "content, created_at, Rating, storeId")] Comment comment, string storeId, HttpPostedFileBase image)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Login", "Account");
            }

            if (ModelState.IsValid)
            {
                using (var db = new FoodcontextDB())
                {
                    // Retrieve the maximum comment_id from the database
                    int maxCommentId = db.Comments.Max(c => c.comment_id);

                    // Increment the comment_id by 1 to generate a new unique ID
                    comment.comment_id = maxCommentId + 1;

                    // Set the user_id and Store_id
                    comment.user_id = User.Identity.GetUserId();
                    comment.Store_id = storeId;
                    comment.created = DateTime.Now;

                    // Handle the uploaded image
                    if (image != null && image.ContentLength > 0)
                    {
                        // Save the image and get the image path
                        string imagePath = SaveImage(image);

                        // Set the image path in the comment
                        comment.img = imagePath;
                    }

                    db.Comments.Add(comment);
                    await db.SaveChangesAsync();
                }

                return RedirectToAction("StoreProducts", new { id = storeId });
            }

            return View("StoreProducts");
        }

        // Helper method to save the image and return the image path
        private string SaveImage(HttpPostedFileBase image)
        {
            // Ensure the ~/Content/products directory exists, create it if necessary
            string uploadDir = Server.MapPath("~/Content/products");
            if (!Directory.Exists(uploadDir))
            {
                Directory.CreateDirectory(uploadDir);
            }

            // Get a unique filename for the image
            string fileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName);

            // Specify the path where you want to save the image
            string imagePath = Path.Combine(uploadDir, fileName);

            // Save the image to the specified path
            image.SaveAs(imagePath);

            // Return the relative image path
            return "/Content/products/" + fileName;
        }



        public ActionResult ShowComment(string storeId)
        {
            storeId = getIdStore;
            using (var db = new FoodcontextDB())
            {
                var comments = db.Comments.Where(c => c.Store_id == storeId).ToList();
                return View(comments);
            }
        }


        public ActionResult HotProducts(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new ApplicationDbContext())
            {
                var products = db.Products.Where(p => p.Userid == id && p.is_hot == true).ToList();
                return View(products);
            }
        }

        public async Task<ActionResult> Sale1(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var db = new ApplicationDbContext())
            {
                var products = db.Products.Where(p => p.Userid == id && p.DiscountPercent != null).ToList();
                return View(products);
            }
        }



        public async Task<ActionResult> Indexchat()
        {

            var userId = User.Identity.GetUserId();

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userMessages = await db.Messages
                .Where(m => m.Userid == userId)
                .ToListAsync();

            return View(userMessages);
        }
        
        public async Task<ActionResult> GetMessages(string storeid)
        {
            Session["CurrentStoreId"] = storeid;
            string userid = User.Identity.GetUserId();
            try
            {
                var userMessages = await db.Messages
                    .Where(m => m.Storeid == storeid && m.Userid == userid)
                    .OrderBy(m => m.Time)
                    .ToListAsync();

                return Json(new { success = true, listMessage = userMessages }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Error retrieving messages: " + ex.Message);
            }
        }

        public JsonResult postMessage(string message, HttpPostedFileBase image)
        {
            string Userid = User.Identity.GetUserId();
            string storeid = Session["CurrentStoreId"] as string;
            TimeSpan timeDuration = DateTime.Now.TimeOfDay;

            if (storeid != null)
            {



                // Create a new message with message text, user, store, time, and image path
                int max = db.Messages.Max(c => c.Id);
                Message mess = new Message
                {
                    Id = max + 1,
                    Content = message,
                    Userid = User.Identity.GetUserId(),
                    Storeid = storeid,
                    Time = timeDuration,

                };

                db.Messages.Add(mess);
                db.SaveChanges();

                // Tạo kết nối tới SignalR Hub
                var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                hubContext.Clients.User(Userid).SendPrivateMessage(storeid, message);
                hubContext.Clients.All.addNewMessageToPage(storeid, message);

                return Json(new { success = true });
            }

            return Json(new { success = false, storeid = storeid });
        }
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public JsonResult postMessage(string message, HttpPostedFileBase newimage)
        //{
        //    string Userid = User.Identity.GetUserId();
        //    string storeid = Session["CurrentStoreId"] as string;
        //    TimeSpan timeDuration = DateTime.Now.TimeOfDay;

        //    if (storeid != null)
        //    {
        //        string imagePath = null; // Initialize imagePath to null

        //        // Check if an image is being uploaded
        //        if (newimage != null && newimage.ContentLength > 0)
        //        {
        //            // Save the image and images the image path
        //            imagePath = SaveImage(newimage);
        //        }

        //        // Create a new message with message text, user, store, time, and image path
        //        int max = db.Messages.Max(c => c.Id);
        //        Message mess = new Message
        //        {
        //            Id = max + 1,
        //            Content = message,
        //            Userid = Userid, // Use Userid, not User.Identity.GetUserId()
        //            Storeid = storeid,
        //            Time = timeDuration,
        //            Img = imagePath // Set the image path
        //        };

        //        db.Messages.Add(mess);
        //        db.SaveChanges();

        //        // Tạo kết nối tới SignalR Hub
        //        var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
        //        hubContext.Clients.User(Userid).SendPrivateMessage(storeid, message);
        //        hubContext.Clients.All.addNewMessageToPage(storeid, message);

        //        return Json(new { success = true });
        //    }

        //    return Json(new { success = false, storeid = storeid });
        //}
    }
}