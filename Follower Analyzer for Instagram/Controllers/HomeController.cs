﻿using Follower_Analyzer_for_Instagram.Models;
using Follower_Analyzer_for_Instagram.Services.InstagramAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public ActionResult Index(string status = "")
        {
            var viewModel = new IndexViewModel();
            viewModel.Username = string.Empty;
            viewModel.Posts = new List<InstagramPost>();
            ViewBag.RequestState = status;
            return View(viewModel);
        }

        public ActionResult About()
        {
            return View();
        }

        public async Task<JsonResult> AddUserToObservation(string userName)//+
        {
            var errors = new List<string>();
            var observableUser = new ObservableUser();

            if ((await _repository.GetListAsync<ObservableUser>()).ToList().Count() < 4)
            {
                observableUser.InstagramPK = _instaApi.GetPrimaryKeyByUsername(userName);
                observableUser.Username = userName;
                bool added = await _repository.CreateAsync(observableUser);

                if (!added)
                {
                    errors.Add("Не удалось добавить пользователя!");

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

            return Json(new
            {
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
        public async Task<JsonResult> FinishUserMonitoring(int? id)//+
        {
            var observableUsers = (await _repository.GetListAsync<ObservableUser>()).ToList();
            var observableUser = observableUsers.Where(user => user.Id == id).FirstOrDefault();

            bool deleted = await _repository.DeleteAsync<ObservableUser>(observableUser);

            if (!deleted)
            {
                Response.StatusCode = 500;//Internal Server Error
            }
            else
            {
                Response.StatusCode = 200;//Ok
            }

            return Json(string.Empty);
        }


        public ActionResult TopTenLikes(string nameForLikes)
        {
            var posts = _instaApi.GetUserPostsByUsername(nameForLikes);
            var sortPosts = from post in posts orderby post.CountOfLikes descending select post;
            var topTenPosts = new List<InstagramPost>();
            int counter = 0;

            foreach (var post in sortPosts)
            {
                if (post != null)
                {
                    topTenPosts.Add(post);
                }

                if (counter == 9)
                {
                    break;
                }
                counter++;
            }
            return PartialView(topTenPosts);
        }

        public ActionResult TopTenByComments(string nameForComments)
        {
            var posts = _instaApi.GetUserPostsByUsername(nameForComments);
            var sortPosts = from post in posts orderby post.CountOfComments descending select post;
            var topTenPosts = new List<InstagramPost>();
            int counter = 0;

            foreach (var post in sortPosts)
            {
                if (post != null)
                {
                    topTenPosts.Add(post);
                }

                if (counter == 9)
                {
                    break;
                }
                counter++;
            }
            return PartialView(topTenPosts);
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
            if (String.IsNullOrEmpty(userPrimaryKey))
                userPrimaryKey = System.Web.HttpContext.Current.Session["PrimaryKey"].ToString();
            var user = new ApplicationUser();
            user = await _repository.GetAsync<ApplicationUser>(x => x.InstagramPK == userPrimaryKey);
            var subscriptionsStatistics = new SubscriptionsStatisticsViewModel();
            // Get current followers list
            List<ApplicationUser> currentSubscriptionsList = await _instaApi.GetUserSubscriptionsByUsernameAsync(user.Username);
            // Get unsubscribed followers
            foreach (var subscription in user.Subscriptions)
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
            return PartialView(subscriptionsStatistics);
        }

        public async Task<ActionResult> GetObservableUserActivities(string userName)
        {
            var activities = new UserActivityViewModel();
            activities.UserName = userName;
            string primaryKey = _instaApi.GetPrimaryKeyByUsername(userName);
            activities.ProfilePictureUrl = await _instaApi.GetUserProfilePictureUriByPrimaryKeyAsync(primaryKey);
            var userActivities = (await _repository.GetListAsync<UserActivity>(x => x.InitiatorPrimaryKey == primaryKey)).ToList();
            activities.Activities = userActivities;
            return PartialView(activities);
        }

        [HttpGet]
        public ActionResult AddObservableUser()
        {
            return PartialView("_AddObservableUser");
        }

        [HttpPost]
        public async Task<ActionResult> AddObservableUser(ObservableUserViewModel observableUser)
        {
            // The current registered user who uses our application
            string primaryKey = System.Web.HttpContext.Current.Session["PrimaryKey"].ToString();
            var observer = await _repository.GetAsync<ApplicationUser>(u => u.InstagramPK == primaryKey);

            // The user we are going to add to the database
            var user = new ObservableUser();
            user.Username = observableUser.UserName;
            user.InstagramPK = _instaApi.GetPrimaryKeyByUsername(observableUser.UserName);
            user.Observers.Add(observer);

            // Checking the presence of the user we want to add to the database in the database
            var observable = await _repository.GetAsync<ObservableUser>(x => x.InstagramPK == user.InstagramPK);
            if (observable != null)
            {
                // If such a user exists, check the existence of the current user in the list of its observers
                if (observable.Observers.Contains(observer))
                {
                    // If such a observer exists, return a message about it.
                    return RedirectToAction("Index", new { status = "repeat" });
                }                    
                else
                {
                    /* If such a observer does not exist, add it to the collection of observers, 
                     update the data in the database and return a message about the successful operation.*/
                    observable.Observers.Add(observer);
                    await _repository.UpdateAsync<ObservableUser>(observable);
                    observer.ObservableAccounts.Add(observable);
                    await _repository.UpdateAsync<ApplicationUser>(observer);
                    return RedirectToAction("Index", new { status = "success" });
                }
            }

            // Trying to add a new observable user to the database
            if (!await _repository.CreateAsync<ObservableUser>(user))
                return RedirectToAction("Index", new { status = "bad" });

            // Update the data in the database
            observer.ObservableAccounts.Add(user);
            await _repository.UpdateAsync<ApplicationUser>(observer);

            // Return a message about the successful operation
            return RedirectToAction("Index", new { status = "success" });
        }

        [HttpGet]
        public async Task<ActionResult> AddObservableAccountForObservableUser()
        {
            string instaPK = _instaApi.GetCurrentUserPrimaryKey();
            var currentUser = await _repository.GetAsync<ApplicationUser>(x => x.InstagramPK == instaPK);
            var observablePage = new ObservableAccountForObservableUserVM();

            foreach (var observableUser in currentUser.ObservableAccounts)
                observablePage.ObservableUsers.Add(new SelectListItem
                {
                    Text = observableUser.Username,
                    Value = observableUser.Username
                });
            return PartialView("_AddObservableAccountForObservableUser", observablePage);
        }

        [HttpPost]
        public async Task<ActionResult> AddObservablePageForObservableUser(ObservableAccountForObservableUserVM observablePage)
        {
            // Receive the observable user
            var observableUser = new ObservableUser();
            observableUser = await _repository.GetAsync<ObservableUser>(x => x.Username == observablePage.observableUserName);

            // Checking the presence of the target content we want to add to the database in the database
            var page = await _repository.GetAsync<ObservableUser>(x => x.Username == observablePage.TargetContentName);
            if(page != null)
            {
                // If such content exists, check the existence of the observable user in the list of its ActivityInitiators
                if (observableUser != null && page.ActivityInitiators.Contains(observableUser))
                {
                    return RedirectToAction("Index", new { status = "repeat" });
                }
                else
                {
                    /* If such observable user does not exist, add it to the collection of ActivityInitiators, 
                     update the data in the database and return a message about the successful operation.*/
                    page.ActivityInitiators.Add(observableUser);
                    await _repository.UpdateAsync<ObservableUser>(page);
                    observableUser.ObservableUsers.Add(page);
                    await _repository.UpdateAsync<ObservableUser>(observableUser);
                    return RedirectToAction("Index", new { status = "success" });
                }
            }

            // If such content is not in the database, then we initialize the new content
            page = new ObservableUser();
            page.InstagramPK = _instaApi.GetPrimaryKeyByUsername(observablePage.TargetContentName);
            page.Username = observablePage.TargetContentName;
            page.ActivityInitiators.Add(observableUser);

            // Trying to add a new content to the database
            if (!await _repository.CreateAsync<ObservableUser>(page))
                return RedirectToAction("Index", new { status = "bad" });

            // Update the data in the database
            observableUser.ObservableUsers.Add(page);
            await _repository.UpdateAsync<ObservableUser>(observableUser);

            // Return a message about the successful operation
            return RedirectToAction("Index", new { status = "success" });
        }
    }
}