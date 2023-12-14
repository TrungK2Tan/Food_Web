using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Food_Web.Models;
using System.IO;

namespace Food_Web.Areas.Store.Controllers
{
    public class CategoriesController : Controller
    {
        private FoodcontextDB db = new FoodcontextDB();

        // GET: Store/Categories
        public async Task<ActionResult> Index()
        {
            return View(await db.Categories.ToListAsync());
        }

        // GET: Store/Categories/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Category category = await db.Categories.FindAsync(id);
            if (category == null)
            {
                return HttpNotFound();
            }
            return View(category);
        }

        // GET: Store/Categories/Create
        public ActionResult Create()
        {
            return View();
        }

        [HttpPost]
        //[ValidateAntiForgeryToken]
        public ActionResult Create(Category category, HttpPostedFileBase Content)
        {

            var context = new FoodcontextDB();

            if (ModelState.IsValid)
            {

                category = context.Categories.Add(category);

                if (Content != null && Content.ContentLength > 0)
                {
                    var typeFile = Path.GetExtension(Content.FileName);
                    category.image = category.Categoryid + typeFile;
                    var filePath = Path.Combine(Server.MapPath("~/Content/products"), category.image);
                    Content.SaveAs(filePath);

                }

                context.SaveChanges();
                return RedirectToAction("Index");
            }
            return View("Create", category);
        }

    }
}
