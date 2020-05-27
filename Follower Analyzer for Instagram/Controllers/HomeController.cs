﻿using Follower_Analyzer_for_Instagram.Models;
using Follower_Analyzer_for_Instagram.Models.DBInfrastructure;
using Follower_Analyzer_for_Instagram.Models.ViewModels;
using Follower_Analyzer_for_Instagram.Services.InstagramAPI;
using InstagramApiSharp.Classes;
using Microsoft.Ajax.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;

namespace Follower_Analyzer_for_Instagram.Controllers
{
    public class HomeController : Controller
    {
        private IRepository _repository;
        private IInstagramAPI _instaApi;

        public HomeController(IRepository repository, IInstagramAPI instaApi)
        {
            _repository = repository;
            InitializeInstaAPI(instaApi);
        }

        private void InitializeInstaAPI(IInstagramAPI instaApi)
        {
            _instaApi = instaApi;

            if (System.Web.HttpContext.Current.Session["PrimaryKey"] == null)
            {
                return;
            }

            string currentUserPrimaryKey = System.Web.HttpContext.Current.Session["PrimaryKey"].ToString();

            if (!string.IsNullOrEmpty(currentUserPrimaryKey))
            {
                _instaApi.SetCookies(GetInstagramCookiesByUserPrimaryKey(currentUserPrimaryKey));
            }
        }

        private byte[] GetInstagramCookiesByUserPrimaryKey(string primaryKey)
        {
            //User user = repository.GetAsync<User>(u => u.InstagramPK == primaryKey).Result;
            var user = new ApplicationUser();

            using (var dbContext = new FollowerAnalyzerContext())
            {
                user = dbContext.ApplicationUsers.First(u => u.InstagramPK == primaryKey);
            }

            return user == null ? new byte[] { } : user.StateData;
        }

        public ActionResult Index()
        {
            var viewModel = new IndexViewModel();
            viewModel.Username = string.Empty;
            viewModel.Posts = new List<InstagramPost>();

            return View(viewModel);
        }

        public ActionResult About()
        {
            return View();
        }
   
        public async Task< JsonResult> AddUserToObservation(string userName)//+
        {
            List<string> errors = new List<string>();
            ObservableUser observableUser = new ObservableUser();

            if ((await _repository.GetListAsync<ObservableUser>()).ToList().Count() < 4)
            {
                observableUser.InstagramPK = _instaApi.GetPrimaryKeyByUsername(userName);
                observableUser.Username = userName;
                bool added = await _repository.CreateAsync(observableUser);

                if (!added)
                {
                    errors.Add( "Не удалось добавить пользователя!");
                 
                    Response.StatusCode = 404;//not found
                }
                else
                {
                    Response.StatusCode = 200;//ok
                }
            }
            else
            {
                errors.Add("Не удалось добавить пользователя!");
                errors.Add("Максимальное количесво пользователей за которыми возможно наблюдение [3]!");
                observableUser = null;
                Response.StatusCode = 303;//See Other 

            }

            return Json(new {
                observableUser = observableUser,
                Errors = errors
            });
        }

        public JsonResult GetSubscribersCurrentUser()//+
        {
           List<ApplicationUser> subscribers = _instaApi.GetUserFollowersByUsername(Session["UserName"].ToString());
          
            return Json(subscribers);
        }

         /// <summary>
        /// завершить наблюдения за пользователем
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task< JsonResult> FinishUserMonitoring(int? id)//+
        {
            var observableUsers = (await _repository.GetListAsync<ObservableUser>()).ToList());
            var observableUser = observableUsers.Where(user => user.Id == id).FirstOrDefault();

            bool deleted = await _repository.DeleteAsync<ObservableUser>(observableUser);

            if(!deleted)
            {
                Response.StatusCode = 500;//Internal Server Error
            }
            else
            {
                Response.StatusCode = 200;//Ok
            }

            return Json(string.Empty);
        }


        public ActionResult TopTenLikes(string userName)
        {
            var posts = _instaApi.GetUserPostsByUsername(userName);
            var sortPosts = from post in posts orderby post.CountOfLikes select post;
            var topTenPosts = new List<InstagramPost>();
            int counter = 0;

            foreach (var post in sortPosts)
            {
                if (post != null)
                {
                    topTenPosts.Add(post);
                }

                if (counter == 10)
                {
                    break;
                }
                counter++;
            }
            return View("ListPosts", topTenPosts);
        }

        public ActionResult TopTenByComments(string userName)
        {
            var posts = _instaApi.GetUserPostsByUsername(userName);
            var sortPosts = from post in posts orderby post.CountOfComments select post;
            var topTenPosts = new List<InstagramPost>();
            int counter = 0;

            foreach (var post in sortPosts)
            {
                if (post != null)
                {
                    topTenPosts.Add(post);
                }

                if (counter == 10)
                {
                    break;
                }
                counter++;
            }
            return View("ListPosts", topTenPosts);
        }

        public ActionResult SortingPostsDescOrder()
        {
            string currentUserPrimaryKey = Session["PrimaryKey"].ToString();
            var posts = _instaApi.GetUserPostsByUsername(Session["UserName"].ToString(), GetInstagramCookiesByUserPrimaryKey(currentUserPrimaryKey));
            var sortPosts = from post in posts orderby post.CountOfComments select post;
            var topTenPosts = new List<InstagramPost>();
            int counter = 0;

            foreach (var post in sortPosts)
            {
                if (post != null)
                {
                    topTenPosts.Add(post);
                }

                if (counter == 10)
                {
                    break;
                }
                counter++;
            }
            return View("ViewName", topTenPosts);
        }

        [HttpGet]
        public ActionResult GetMostPopularPosts(IndexViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            List<InstagramPost> posts = _instaApi.GetUserPostsByUsername(viewModel.Username);
            viewModel.Posts = new List<InstagramPost>();

            if (posts.Count == 0)
            {
                return View("Index", viewModel);
            }

            //var sortedByLikesPosts = from post in posts orderby post.CountOfLikes select post;
            posts.Sort(
                (post1, post2) => post1.CountOfLikes.CompareTo(post2.CountOfLikes)
                );
            viewModel.Posts.Add(posts.Last());

            posts.Sort(
                (post1, post2) => post1.CountOfComments.CompareTo(post2.CountOfComments)
                );
            //var sortedByCommentsPosts = from post in posts orderby post.CountOfComments select post;
            viewModel.Posts.Add(posts.Last());

            return View("Index", viewModel);
        }

        public async Task<ActionResult> GetSubscriptionsStatisticsAsync(string userPrimaryKey = null)
        {
            if(String.IsNullOrEmpty(userPrimaryKey))
                userPrimaryKey = System.Web.HttpContext.Current.Session["PrimaryKey"].ToString();
            var user = new ApplicationUser();
            user = await _repository.GetAsync<ApplicationUser>(x => x.InstagramPK == userPrimaryKey);
            var subscriptionsStatistics = new SubscriptionsStatisticsViewModel();
            // Get current followers list
            List<ApplicationUser> currentSubscriptionsList = await _instaApi.GetUserSubscriptionsByUsernameAsync(user.Username);
            // Get unsubscribed followers
            foreach(var subscription in user.Subscriptions)
            {
                if (!currentSubscriptionsList.Contains(subscription))
                    subscriptionsStatistics.UnsubscribedSubscriptions.Add(subscription);
            }
            // Get new followers
            foreach (var subscription in currentSubscriptionsList)
            {
                if (!user.Subscriptions.Contains(subscription))
                    subscriptionsStatistics.NewSubscriptions.Add(subscription);
            }
            //If there are changes, then save them in the database
            if (subscriptionsStatistics.NewSubscriptions.Count > 0 || subscriptionsStatistics.UnsubscribedSubscriptions.Count > 0)
            { 
                user.Subscriptions = currentSubscriptionsList;
                await _repository.UpdateAsync<ApplicationUser>(user);
            }
            return PartialView("_SubscriptionsStatistics", subscriptionsStatistics);
        }
    }
}