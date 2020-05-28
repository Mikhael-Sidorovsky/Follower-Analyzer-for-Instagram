﻿using Follower_Analyzer_for_Instagram.Models;
using Follower_Analyzer_for_Instagram.Models.DBInfrastructure;
using Follower_Analyzer_for_Instagram.Models.ViewModels;
using Follower_Analyzer_for_Instagram.Services.InstagramAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
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

        public ActionResult GetMostPopularPosts(string name)
        {
            var viewModel = new IndexViewModel();
            List<InstagramPost> posts = _instaApi.GetUserPostsByUsername(name);
            viewModel.Username = name;

            viewModel.Posts = new List<InstagramPost>();

            if (posts.Count == 0)
            {
                return PartialView(viewModel);
            }

            posts.Sort((post1, post2) => post1.CountOfLikes.CompareTo(post2.CountOfLikes));
            viewModel.Posts.Add(posts.Last());

            posts.Sort((post1, post2) => post1.CountOfComments.CompareTo(post2.CountOfComments));
            viewModel.Posts.Add(posts.Last());

            return PartialView(viewModel);
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