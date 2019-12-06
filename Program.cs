using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
//using System.Threading;
//using System.Threading.Tasks;
//using System.Windows.Forms;

namespace HackerNews
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Please wait fetching posts..");
			var posts = GetPosts();
			var users = posts.Select(post => post.User).ToList();
			var topPost = default(Post);

			if (posts.Count > 0)
			{
				topPost = posts.OrderByDescending(post => post.CommentCount).First();
			}

			foreach (var user in users)
			{
				GetUserKarma(user);
			}

			users = users.OrderByDescending(user => user.Karma).ToList();
			var topUser = default(User);
			if (users.Count > 0)
			{
				topUser = users.First();
			}

			while (true)
			{
				Console.WriteLine("Select option ");
				Console.WriteLine("1. Top post by number of comments ");
				Console.WriteLine("2. Top user by Karma ");
				Console.WriteLine("3. Exit");

				var input = Console.ReadLine();
				int option;
				if (!Int32.TryParse(input, out option))
				{
					Console.WriteLine("Please select valid option");
					continue;
				}

				if (option == 1)
				{
					if (topPost != default(Post))
					{
						Console.WriteLine("Titile: {0} || comments: {1}", topPost.Title, topPost.CommentCount);
					}
					else
					{
						Console.WriteLine("No posts found");
						break;
					}
				}

				if (option == 2)
				{
					if (topUser != default(User))
					{
						Console.WriteLine("User : {0} || Karma: {1}", topUser.UserId, topUser.Karma);
					}
					else
					{
						Console.WriteLine("No user found");
						break;
					}
				}

				if (option == 3)
				{
					break;
				}
			}
		}

		static HtmlDocument GetPage(string url)
		{
			HttpClient http = new HttpClient();
			HtmlDocument page = new HtmlDocument();

			try
			{
				var response = http.GetByteArrayAsync(url).Result;
				String source = Encoding.GetEncoding("utf-8").GetString(response, 0, response.Length - 1);
				source = WebUtility.HtmlDecode(source);
				page.LoadHtml(source);
			}
			catch (Exception ex)
			{
				page = null;
			}

			return page;
		}

		static List<Post> GetPosts()
		{
			//HttpClient http = new HttpClient();
			//var response = http.GetByteArrayAsync(@"https://news.ycombinator.com/").Result;
			//String source = Encoding.GetEncoding("utf-8").GetString(response, 0, response.Length - 1);
			//source = WebUtility.HtmlDecode(source);
			//HtmlDocument resultat = new HtmlDocument();
			//resultat.LoadHtml(source);
			var page = GetPage(@"https://news.ycombinator.com/");
			if (page == null)
				return new List<Post>();

			List<HtmlNode> toftitle = page.DocumentNode.Descendants().Where(x => (x.Name == "tbody")).ToList();

			var postTable = page.DocumentNode.Descendants("table")
				.Where(table => table.Attributes.Contains("class"))
				.SingleOrDefault(table => table.Attributes["class"].Value == "itemlist");

			var rows = postTable.SelectNodes("tr");
			var rowsCount = rows.Count / 2;
			var userMap = new Dictionary<string, User>();
			var posts = new List<Post>();
			var postCount = 0;

			while (postCount < rowsCount)
			{
				var postRows = rows.Skip(postCount * 3).Take(2).ToList();
				if (postRows.Count == 2)
				{
					var titleRow = postRows.First();
					var detailsRow = postRows.Last();

					//get titile from first row 
					var tds = titleRow.SelectNodes("td");
					if (tds == null)
					{
						postCount++;
						continue;
					}

					var titleTDS = tds.Where(td => td.Attributes.Contains("class") && td.Attributes["class"].Value == "title").ToList();
					var post = new Post();

					if (titleTDS.Count > 1)
					{
						post.Title = titleTDS.Last().InnerText;
					}

					var detailsTD = detailsRow.SelectNodes("td").Where(td => td.Attributes.Contains("class") && td.Attributes["class"].Value == "subtext").FirstOrDefault();
					var postMetaDataAnchors = detailsTD.SelectNodes("a");

					if (postMetaDataAnchors.Count > 0)
					{
						var userDetailsAnchor = postMetaDataAnchors.Where(a => a.Attributes.Contains("class") && a.Attributes["class"].Value == "hnuser").FirstOrDefault();
						var userHref = userDetailsAnchor.Attributes.Where(attr => attr.Name == "href").Select(attr => attr.Value).FirstOrDefault();
						if (!string.IsNullOrWhiteSpace(userHref))
						{
							var tokens = userHref.Split('=').ToArray();
							if (tokens.Length > 1)
							{
								var userId = tokens[1];
								User user;
								if (userMap.TryGetValue(userId, out user))
								{
									//user already exist no need to do anything.
								}
								else
								{
									user = new User() { UserId = userId };
									userMap[userId] = user;
								}

								post.User = user;
							}
						}

						var commentsDetailsAnchor = postMetaDataAnchors.Last().InnerText;
						string commentCount = Regex.Replace(commentsDetailsAnchor, @"\D", "");
						post.CommentCount = ParseNumberFromText(commentCount);

						posts.Add(post);
					}
				}

				postCount++;
			}

			return posts;
		}

		static void GetUserKarma(User user)
		{
			var page = GetPage(string.Format("https://news.ycombinator.com/user?id={0}", user.UserId));
			if (page == null)
			{
				return;
			}

			var profileTables = page.DocumentNode.Descendants("table").ToList();
			if (profileTables != null && profileTables.Count == 3)
			{
				var userProfile = profileTables.Last();
				if (userProfile != null)
				{
					var userDetails = userProfile.SelectNodes("tr").ToList();
					//has no specific selectr 3rd tr has karma details
					if (userDetails != null && userDetails.Count >= 3)
					{
						var karmaRow = userDetails.Skip(2).Take(1).FirstOrDefault();
						if (karmaRow != null)
						{
							var karmaTDs = karmaRow.SelectNodes("td");
							if (karmaTDs != null && karmaTDs.Count == 2)
							{
								var karmaText = karmaTDs.Last().InnerText;
								user.Karma = ParseNumberFromText(karmaText);
							}
						}
					}
				}
			}
		}

		static int ParseNumberFromText(string commentsDetailsAnchor)
		{
			string commentCount = Regex.Replace(commentsDetailsAnchor, @"\D", "");
			int _commentCount = 0;
			if (!string.IsNullOrWhiteSpace(commentCount) && Int32.TryParse(commentCount, out _commentCount))
			{
			}

			return _commentCount;
		}
	}

	public class Post
	{
		public string Title { get; set; }

		public User User { get; set; }

		public int Points { get; set; }

		public int CommentCount { get; set; }

		public override string ToString()
		{
			var titleStr = "";
			if (!string.IsNullOrWhiteSpace(this.Title) && this.Title.Length > 15)
			{
				titleStr = this.Title.Substring(0, 14) + "...";
			}

			if (this.User != null && !string.IsNullOrWhiteSpace(this.User.UserId))
			{
				titleStr = titleStr + " by " + this.User.UserId;
			}

			return string.Format("{0} Comments Count: {1}", titleStr, this.CommentCount);
		}
	}

	public class User
	{
		public string UserId { get; set; }

		public int Karma { get; set; }
	}
}
