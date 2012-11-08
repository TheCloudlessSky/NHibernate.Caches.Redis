using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using NHibernate.Caches.Redis.Sample.Models;

namespace NHibernate.Caches.Redis.Sample.Controllers
{
    public class HomeController : Controller
    {
        private ISession session;

        [HttpGet]
        public ActionResult Index()
        {
            var posts = session.QueryOver<BlogPost>().Cacheable().List();
            return View(posts);
        }

        [HttpPost]
        public ActionResult Create(string title, string body)
        {
            session.Save(new BlogPost()
            {
                Title = title,
                Body = body,
                Created = DateTime.Now
            });
            return RedirectToAction("index");
        }

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            base.OnActionExecuting(filterContext);
            MvcApplication.SessionFactory.Statistics.Clear();
            this.session = MvcApplication.SessionFactory.OpenSession();
            this.session.BeginTransaction();
        }

        protected override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            this.session.Transaction.Commit();
            this.session.Dispose();
            base.OnResultExecuted(filterContext);
        }
    }
}
