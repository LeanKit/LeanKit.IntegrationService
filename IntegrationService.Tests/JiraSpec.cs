//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright> 
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using IntegrationService.Targets.JIRA;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using Moq;
using NUnit.Framework;
using RestSharp;
using ServiceStack.Text;
using Should;
using Ploeh.SemanticComparison.Fluent;

namespace IntegrationService.Tests.JIRA
{
    public class JiraSpec : IntegrationBaseSpec
    {
		protected Mock<IRestClient> MockRestClient;
		protected IRestClient RestClient;
	    protected new Jira TestItem;

	    protected override void OnCreateMockObjects()
	    {
			base.OnCreateMockObjects();
		    MockRestClient = new Mock<IRestClient>();
			RestClient = MockRestClient.Object;
	    }

	    protected override void OnArrange()
        {
            MockConfigurationProvider.Setup(x => x.GetConfiguration()).Returns(TestConfig);
            MockLeanKitClientFactory.Setup(x => x.Create(It.IsAny<LeanKitAccountAuth>())).Returns(LeanKitApi);
		    MockRestClient.Setup(x => x.Execute(It.IsAny<IRestRequest>())).Returns((IRestResponse)null);
        }

        protected override void OnStartTest()
        {
            TestItem = new Jira(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
        }

    }

    [TestFixture]
    public class When_starting_with_a_valid_configuration : JiraSpec
    {
        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
        }

        protected override void OnArrange()
        {
            base.OnArrange();
            MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<int>())).Returns(new Board());
        }

        [Test]
        public void It_should_get_board_for_each_mapping()
        {
            MockLeanKitApi.Verify(x=>x.GetBoard(It.IsAny<long>()),Times.Exactly(TestConfig.Mappings.Count));
        }
    }

    [TestFixture]
    public class When_starting_with_an_invalid_configuration : JiraSpec
    {
        protected override void OnArrange()
        {
            MockConfigurationProvider.Setup(x => x.GetConfiguration()).Throws<ConfigurationErrorsException>();
        }
        
        [Test]
        public void It_should_not_attempt_to_load_app_settings()
        {
            MockLocalStorage.Verify(x=>x.Load(), Times.Never());
        }

        [Test]
        public void It_should_not_attempt_to_connect_to_leankit()
        {
            MockLeanKitClientFactory.Verify(x => x.Create(It.IsAny<LeanKitAccountAuth>()), Times.Never());
        }
    }

    [TestFixture]
    public class When_starting_with_valid_leankit_acount : JiraSpec
    {
        private LeanKitAccountAuth TestAuth;
        protected override void OnStartFixture()
        {
            TestConfig = Test<Configuration>.Item;
            TestAuth = new LeanKitAccountAuth
            {
                Hostname = TestConfig.LeanKit.Url,
				UrlTemplateOverride = "http://{0}.leankit.com",
                Username = TestConfig.LeanKit.User,
                Password = TestConfig.LeanKit.Password
            };

        }

        [Test]
        public void It_should_use_configured_credentials_for_leankit()
        {
            var likeness = TestAuth.AsSource().OfLikeness<LeanKitAccountAuth>();
            MockLeanKitClientFactory.Verify(x => x.Create(It.Is<LeanKitAccountAuth>(auth => likeness.Equals(auth))));
        }
    }

	[TestFixture]
	public class When_calculating_priority : JiraSpec
	{
		protected override void OnStartFixture() 
		{
			TestConfig = Test<Configuration>.Item;
		}

		[Test]
		public void It_should_default_to_normal()
		{
			JiraConversionExtensions.CalculateLeanKitPriority((Jira.Issue)null).ShouldEqual(1);
		}

		[Test]
		public void It_should_map_priority_of_critical_to_critical()
		{
			JiraConversionExtensions.CalculateLeanKitPriority(new Jira.Issue()
				{
					Fields = new Jira.Fields() { Priority = new Jira.Priority() { Name = "Critical"}}
				}).ShouldEqual(3);
		}

		[Test]
		public void It_should_map_priority_of_blocker_to_critical() 
		{
			JiraConversionExtensions.CalculateLeanKitPriority(new Jira.Issue()
			{
				Fields = new Jira.Fields() { Priority = new Jira.Priority() { Name = "Blocker" } }
			}).ShouldEqual(3);
		}

		[Test]
		public void It_should_map_label_of_major_to_high() 
		{
			JiraConversionExtensions.CalculateLeanKitPriority(new Jira.Issue()
			{
				Fields = new Jira.Fields() { Priority = new Jira.Priority() { Name = "Major" } }				
			}).ShouldEqual(2);
		}

		[Test]
		public void It_should_map_label_of_minor_to_normal() 
		{
			JiraConversionExtensions.CalculateLeanKitPriority(new Jira.Issue()
			{
				Fields = new Jira.Fields() { Priority = new Jira.Priority() { Name = "Minor" } }
			}).ShouldEqual(1);
		}

		[Test]
		public void It_should_map_label_of_trivial_to_low() 
		{
			JiraConversionExtensions.CalculateLeanKitPriority(new Jira.Issue()
			{
				Fields = new Jira.Fields() { Priority = new Jira.Priority() { Name = "Trivial" } }
			}).ShouldEqual(0);
		}

		[Test]
		public void It_should_map_label_of_anything_else_to_normal() 
		{
			JiraConversionExtensions.CalculateLeanKitPriority(new Jira.Issue()
			{
				Fields = new Jira.Fields() { Priority = new Jira.Priority() { Name = "Bob" } }
			}).ShouldEqual(1);
		}
	}

	[TestFixture]
	public class When_calculating_card_type : JiraSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			foreach (var cardType in _testBoard.CardTypes)
				cardType.IsDefault = false;
			_testBoard.CardTypes.Add(new CardType() { Id = 999, Name = "Willy", IsDefault = false });
			_testBoard.CardTypes.Last().IsDefault = true;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.Types = new List<WorkItemType>() { new WorkItemType() { LeanKit = "Willy", Target = "Roger" } };
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_return_default_card_type_if_issue_has_no_priority() 
		{
			JiraConversionExtensions.CalculateLeanKitCardType(_mapping, null).Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
			JiraConversionExtensions.CalculateLeanKitCardType(_mapping, "").Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
		}

		[Test]
		public void It_should_return_default_card_type_if_issue_has_no_matching_priority() 
		{
			JiraConversionExtensions.CalculateLeanKitCardType(_mapping, "Bob").Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
		}

		[Test]
		public void It_should_return_implicit_card_type_if_issue_has_matching_priority() 
		{
			JiraConversionExtensions.CalculateLeanKitCardType(_mapping, "Willy").Id.ShouldEqual(_testBoard.CardTypes.FirstOrDefault(x => x.Name == "Willy").Id);
		}

		[Test]
		public void It_should_return_mapped_card_type_if_issue_has_matching_label() 
		{
			JiraConversionExtensions.CalculateLeanKitCardType(_mapping, "Roger").Id.ShouldEqual(_testBoard.CardTypes.FirstOrDefault(x => x.Name == "Willy").Id);
		}

	}

	[TestFixture]
	public class When_calculating_assigned_user : JiraSpec
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			int ctr = 0;
			foreach (var boardUser in _testBoard.BoardUsers)
			{
				if (ctr == 0)
				{
					boardUser.UserName = "jcash";
					boardUser.FullName = "Johnny Cash";
					boardUser.EmailAddress = "johnny@cash.com";
					boardUser.Id = 101;
				}
				ctr++;
			}
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_return_userid_on_matched_username()
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit, 
				new Jira.Issue()
			{
				Fields = new Jira.Fields() { Assignee = new Jira.Author() { Name = "jCash" } }
			}, LeanKitApi).ShouldEqual(101);
		}

		[Test]
		public void It_should_return_null_on_nonmatched_username() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { Name = "willyb" } }
				}, LeanKitApi).ShouldBeNull();
		}

		[Test]
		public void It_should_return_null_on_empty_username() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { Name = "" } }
				}, LeanKitApi).ShouldBeNull();
		}


		[Test]
		public void It_should_return_userid_on_matched_email() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { EmailAddress = "Johnny@Cash.com"} }
				}, LeanKitApi).ShouldEqual(101);
		}

		[Test]
		public void It_should_return_null_on_nonmatched_email() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { EmailAddress = "willyB@Cash.com" } }
				}, LeanKitApi).ShouldBeNull();
		}

		[Test]
		public void It_should_return_null_on_empty_email() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { EmailAddress = "" } }
				}, LeanKitApi).ShouldBeNull();
		}

		[Test]
		public void It_should_return_userid_on_matched_fullname() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { DisplayName = "Johnny Cash" } }
				}, LeanKitApi).ShouldEqual(101);
		}

		[Test]
		public void It_should_return_null_on_nonmatched_fullname() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { DisplayName = "Willy Cash" } }
				}, LeanKitApi).ShouldBeNull();
		}

		[Test]
		public void It_should_return_null_on_empty_fullname() 
		{
			JiraConversionExtensions.CalculateLeanKitAssignedUserId(_mapping.Identity.LeanKit,
				new Jira.Issue()
				{
					Fields = new Jira.Fields() { Assignee = new Jira.Author() { DisplayName = "" } }
				}, LeanKitApi).ShouldBeNull();
		}

	}

	public class When_creating_a_target_item : JiraSpec
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		private class TestJira : Jira
		{
			public TestJira(IBoardSubscriptionManager subscriptions,
								IConfigurationProvider<Configuration> configurationProvider,
								ILocalStorage<AppSettings> localStorage,
								ILeanKitClientFactory leanKitClientFactory,
								IRestClient restClient)
				: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory, restClient) { }

			public void TestCreateNewItem(Card card, BoardMapping boardMapping) 
			{
				base.CreateNewItem(card, boardMapping);
			}
		}

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add(1, new List<string>{ "open" });
			_mapping.LaneToStatesMap.Add(2, new List<string> { "closed" });
			_mapping.ValidCardTypes = new List<CardType>() { new CardType() { Id = 1, Name = "Bug"}};
            _mapping.CreateTargetItems = true;
            TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Jira.Issue>();

			var issue1 = new Jira.Issue()
			{
				Id = 1,
				Key = "one",
				Fields = new Jira.Fields()
				{
					Status = new Jira.Status() { Name = "Open" },
					Description = "Issue 1",
					Summary = "Issue 1"
				}
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(issue1), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue") && y.Method == Method.POST))).Returns(restResponse1);			
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestJira(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_jira_to_create_issue() 
		{
			Card card = new Card();
			card.ExternalCardID = "one";
			card.ExternalSystemName = "Jira";
			card.Description = "Issue 1 Description";
			card.Title = "Issue 1 Title";
			card.TypeName = "Bug";
			card.TypeId = 1;

			((TestJira)TestItem).TestCreateNewItem(card, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue") && y.Method == Method.POST)), Times.Exactly(1));
		}

	}

	public class When_updating_properties_of_target_item : JiraSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		public class TestJira : Jira 
		{
			public TestJira(IBoardSubscriptionManager subscriptions,
								IConfigurationProvider<Configuration> configurationProvider,
								ILocalStorage<AppSettings> localStorage,
								ILeanKitClientFactory leanKitClientFactory,
								IRestClient restClient)
				: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory, restClient) { }

			public void TestCardUpdated(Card card, List<string> updatedItems, BoardMapping boardMapping) 
			{
				base.CardUpdated(card, updatedItems, boardMapping);
			}
		}

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add(1, new List<string> { "open" });
			_mapping.LaneToStatesMap.Add(2, new List<string> { "closed" });
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Jira.Issue>();

			var issue1 = new Jira.Issue()
			{
				Id = 1,
				Key = "one",
				Fields = new Jira.Fields()
					{
						Status = new Jira.Status() { Name = "Open" }, 
						Description = "Issue 1", 
						Summary = "Issue 1"
					}
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(issue1), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/one") && y.Method == Method.GET))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/one") && y.Method == Method.PUT))).Returns(restResponse1);

			var issue2 = new Jira.Issue()
			{
				Id = 2,
				Key = "two", 
				Fields = new Jira.Fields()
				{
					Status = new Jira.Status() { Name = "Open" },
					Description = "Issue 2",
					Summary = "Issue 2"
				}
			};

			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(issue2), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.GET))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.PUT))).Returns(restResponse2);

			var issue3 = new Jira.Issue()
			{
				Id = 3,
				Key = "three",
				Fields = new Jira.Fields()
				{
					Status = new Jira.Status() { Name = "Open" },
					Description = "Issue 3",
					Summary = "Issue 3"
				}
			};

			var restResponse3 = new RestResponse() { Content = serializer.SerializeToString(issue3), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/three") && y.Method == Method.GET))).Returns(restResponse3);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/three") && y.Method == Method.PUT))).Returns(restResponse3);

			var issue4= new Jira.Issue()
			{
				Id = 4,
				Key = "four",
				Fields = new Jira.Fields()
				{
					Status = new Jira.Status() { Name = "Open" },
					Description = "Issue 4",
					Summary = "Issue 4"
				}
			};

			var restResponse4 = new RestResponse() { Content = serializer.SerializeToString(issue4), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/four") && y.Method == Method.GET))).Returns(restResponse4);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/four") && y.Method == Method.PUT))).Returns(restResponse4);			
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestJira(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_jira_to_update_issue_if_many_properties_change() 
		{
			Card card = new Card();
			card.ExternalCardID = "one";
			card.ExternalSystemName = "Jira";
			card.Description = "Issue 1 Description";
			card.Title = "Issue 1 Title";

			((TestJira)TestItem).TestCardUpdated(card, new List<string>() { "Title", "Description" }, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/one") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/one") && y.Method == Method.PUT)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_jira_to_update_issue_if_properties_do_not_change() 
		{
			Card card = new Card();
			card.ExternalCardID = "two";
			card.ExternalSystemName = "Jira";
			card.Description = "Issue 2";
			card.Title = "Issue 2";

			((TestJira)TestItem).TestCardUpdated(card, new List<string>() { "Title", "Description" }, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/two") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/two") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_call_jira_to_update_issue_if_one_property_changes() 
		{
			Card card = new Card();
			card.ExternalCardID = "three";
			card.ExternalSystemName = "Jira";
			card.Description = "Issue 3";
			card.Title = "Issue 3 Title";

			((TestJira)TestItem).TestCardUpdated(card, new List<string>() { "Title" }, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/three") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/three") && y.Method == Method.PUT)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_jira_to_update_issue_if_no_identified_properties_change() 
		{
			Card card = new Card();
			card.ExternalCardID = "four";
			card.ExternalSystemName = "Jira";
			card.Description = "Issue 4";
			card.Title = "Issue 4";

			((TestJira)TestItem).TestCardUpdated(card, new List<string>(), _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/four") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/four") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_not_call_jira_to_update_issue_if_externalsystemname_does_not_match() 
		{
			Card card = new Card();
			card.ExternalCardID = "five";
			card.ExternalSystemName = "Jiraboy";
			card.Description = "Issue 5";
			card.Title = "Issue 5";

			((TestJira)TestItem).TestCardUpdated(card, new List<string>(), _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/five") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/latest/issue/five") && y.Method == Method.PUT)), Times.Never());
		}
	}

	public class When_updating_state_of_target_item : JiraSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		public class TestJira : Jira 
		{
			public TestJira(IBoardSubscriptionManager subscriptions,
								IConfigurationProvider<Configuration> configurationProvider,
								ILocalStorage<AppSettings> localStorage,
								ILeanKitClientFactory leanKitClientFactory,
								IRestClient restClient)
				: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory, restClient) { }

			public void TestUpdateStateOfExternalItem(Card card, List<String> laneStateMap, BoardMapping boardConfig) 
			{
				base.UpdateStateOfExternalItem(card, laneStateMap, boardConfig, true);
			}
		}

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add(1, new List<string> { "open" });
			_mapping.LaneToStatesMap.Add(2, new List<string> { "closed" });
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Jira.Issue>();

			var issue1 = new Jira.Issue()
			{
				Id = 1,
				Key = "one",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Open" }}
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(issue1), StatusCode = HttpStatusCode.OK };

			var transitions1 = new Jira.TransitionsResponse()
				{
					Transitions = new List<Jira.Transition>() { new Jira.Transition() { Id = "2", Name = "Closed", To = new Jira.Status() { Name = "Closed", Description = "Closed", Id = "2" }}}
				};

			var transitionsSerializer = new JsonSerializer<Jira.TransitionsResponse>();
			var restTransitionsResponse1 = new RestResponse() { Content = transitionsSerializer.SerializeToString(transitions1), StatusCode = HttpStatusCode.OK};

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/one") && y.Method == Method.GET))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/one/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/one/transitions") && y.Method == Method.POST))).Returns(restResponse1);

			var issue2 = new Jira.Issue()
			{
				Id = 2,
				Key = "two",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Closed"}}
			};

			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(issue2), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.GET))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/two/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/two/transitions") && y.Method == Method.POST))).Returns(restResponse2);

			var errorSerializer = new JsonSerializer<Jira.ErrorMessage>();
			var errorResponse = new RestResponse() { Content = errorSerializer.SerializeToString(new Jira.ErrorMessage() { Message = "Error" }), StatusCode = HttpStatusCode.NotFound };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/three") && y.Method == Method.GET))).Returns(errorResponse);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/three/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/three/transitions") && y.Method == Method.POST))).Returns(errorResponse);

			var issue4 = new Jira.Issue()
			{
				Id = 4,
				Key = "four",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Open" } }
			};

			var restResponse4 = new RestResponse() { Content = serializer.SerializeToString(issue4), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/four") && y.Method == Method.GET))).Returns(restResponse4);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/four/transitions") && y.Method == Method.GET))).Returns(errorResponse);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/four/transitions") && y.Method == Method.POST))).Returns(restResponse4);

			var transitions2 = new Jira.TransitionsResponse()
			{
				Transitions = new List<Jira.Transition>() { new Jira.Transition() { Id = "3", Name = "Resolved", To = new Jira.Status() { Name = "Resolved", Description = "Resolved", Id = "3" } } }
			};

			var restTransitionsResponse2 = new RestResponse() { Content = transitionsSerializer.SerializeToString(transitions2), StatusCode = HttpStatusCode.OK };

			var issue5 = new Jira.Issue()
			{
				Id = 5,
				Key = "five",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Open" } }
			};

			var restResponse5 = new RestResponse() { Content = serializer.SerializeToString(issue5), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/five") && y.Method == Method.GET))).Returns(restResponse5);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/five/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/five/transitions") && y.Method == Method.POST))).Returns(restResponse5);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestJira(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_jira_to_update_ticket_if_ticket_state_is_not_end_state() 
		{
			Card card = new Card() { Id = 1, ExternalSystemName = "Jira", ExternalCardID = "one" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/one") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/one/transitions") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/one/transitions") && y.Method == Method.POST)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_jira_to_update_ticket_state_is_already_end_state() 
		{
			Card card1 = new Card() { Id = 1, ExternalSystemName = "Jira", ExternalCardID = "one" };
			Card card2 = new Card() { Id = 2, ExternalSystemName = "Jira", ExternalCardID = "two" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card1, _mapping.LaneToStatesMap[2], _mapping);
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card2, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/two/transitions") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/two/transitions") && y.Method == Method.POST)), Times.Never());
		}

		[Test]
		public void It_should_not_call_jira_to_update_ticket_if_card_does_not_have_external_id() 
		{
			Card card1 = new Card() { Id = 1, ExternalSystemName = "Jira", ExternalCardID = "one" };
			Card card2 = new Card() { Id = 2, ExternalSystemName = "Jira", ExternalCardID = "" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card1, _mapping.LaneToStatesMap[2], _mapping);
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card2, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/two/transitions") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/two/transitions") && y.Method == Method.POST)), Times.Never());
		}

		[Test]
		public void It_should_not_call_jira_to_update_ticket_if_jira_does_not_have_matching_issue() 
		{
			Card card = new Card() { Id = 3, ExternalSystemName = "Jira", ExternalCardID = "three" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/three") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/three/transitions") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/three/transitions") && y.Method == Method.POST)), Times.Never());
		}

		[Test]
		public void It_should_not_call_jira_to_update_ticket_if_jira_does_not_return_transitions() 
		{
			Card card = new Card() { Id = 4, ExternalSystemName = "Jira", ExternalCardID = "four" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/four") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/four/transitions") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/four/transitions") && y.Method == Method.POST)), Times.Never());
		}

		[Test]
		public void It_should_not_call_jira_to_update_ticket_if_jira_does_not_return_valid_transition() 
		{
			Card card = new Card() { Id = 5, ExternalSystemName = "Jira", ExternalCardID = "five" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/five") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/five/transitions") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/five/transitions") && y.Method == Method.POST)), Times.Never());
		}

		[Test]
		public void It_should_not_call_jira_to_update_ticket_if_externalsystemname_does_not_match() 
		{
			Card card = new Card() { Id = 6, ExternalSystemName = "Jirabob", ExternalCardID = "six" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/siz") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/six/transitions") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/six/transitions") && y.Method == Method.POST)), Times.Never());
		}
	}

	public class When_updating_state_of_target_item_through_workflow : JiraSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		public class TestJira : Jira 
		{
			public TestJira(IBoardSubscriptionManager subscriptions,
								IConfigurationProvider<Configuration> configurationProvider,
								ILocalStorage<AppSettings> localStorage,
								ILeanKitClientFactory leanKitClientFactory,
								IRestClient restClient)
				: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory, restClient) { }

			public void TestUpdateStateOfExternalItem(Card card, List<string> laneStateMap, BoardMapping boardConfig) 
			{
				base.UpdateStateOfExternalItem(card, laneStateMap, boardConfig, true);
			}
		}

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.LaneToStatesMap.Add(1, new List<string> { "open" });
			_mapping.LaneToStatesMap.Add(2, new List<string> { "active>resolved>closed" });
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Jira.Issue>();

			var issue1 = new Jira.Issue()
			{
				Id = 1,
				Key = "one",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Open" } }
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(issue1), StatusCode = HttpStatusCode.OK };

			var transitions1 = new Jira.TransitionsResponse()
			{
				Transitions = new List<Jira.Transition>()
					{
						new Jira.Transition() { Id = "3", Name = "Active", To = new Jira.Status() { Name = "Active", Description = "Active", Id = "3" } },
						new Jira.Transition() { Id = "4", Name = "Resolved", To = new Jira.Status() { Name = "Resolved", Description = "Resolved", Id = "4" } },
						new Jira.Transition() { Id = "2", Name = "Closed", To = new Jira.Status() { Name = "Closed", Description = "Closed", Id = "2" } }
					}
			};

			var transitionsSerializer = new JsonSerializer<Jira.TransitionsResponse>();
			var restTransitionsResponse1 = new RestResponse() { Content = transitionsSerializer.SerializeToString(transitions1), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/one") && y.Method == Method.GET))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/one/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/one/transitions") && y.Method == Method.POST))).Returns(restResponse1);

			var issue2 = new Jira.Issue()
			{
				Id = 2,
				Key = "two",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Open" } }
			};

			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(issue2), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.GET))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/two/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/two/transitions") && y.Method == Method.POST))).Returns(restResponse2);

			var issue3 = new Jira.Issue()
			{
				Id = 3,
				Key = "three",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Active" } }
			};

			var restResponse3 = new RestResponse() { Content = serializer.SerializeToString(issue3), StatusCode = HttpStatusCode.OK };

			var transitions3 = new Jira.TransitionsResponse()
			{
				Transitions = new List<Jira.Transition>()
					{						
						new Jira.Transition() { Id = "4", Name = "Resolved", To = new Jira.Status() { Name = "Resolved", Description = "Resolved", Id = "4" } },
						new Jira.Transition() { Id = "2", Name = "Closed", To = new Jira.Status() { Name = "Closed", Description = "Closed", Id = "2" } }
					}
			};

			var restTransitionsResponse3 = new RestResponse() { Content = transitionsSerializer.SerializeToString(transitions3), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/three") && y.Method == Method.GET))).Returns(restResponse3);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/three/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse3);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/three/transitions") && y.Method == Method.POST))).Returns(restResponse3);


			var issue4 = new Jira.Issue()
			{
				Id = 4,
				Key = "four",
				Fields = new Jira.Fields() { Status = new Jira.Status() { Name = "Resolved" } }
			};

			var restResponse4 = new RestResponse() { Content = serializer.SerializeToString(issue4), StatusCode = HttpStatusCode.OK };

			var transitions4 = new Jira.TransitionsResponse()
			{
				Transitions = new List<Jira.Transition>()
					{						
						new Jira.Transition() { Id = "3", Name = "Active", To = new Jira.Status() { Name = "Active", Description = "Active", Id = "3" } },
						new Jira.Transition() { Id = "2", Name = "Closed", To = new Jira.Status() { Name = "Closed", Description = "Closed", Id = "2" } }
					}
			};

			var restTransitionsResponse4 = new RestResponse() { Content = transitionsSerializer.SerializeToString(transitions4), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/four") && y.Method == Method.GET))).Returns(restResponse4);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/four/transitions") && y.Method == Method.GET))).Returns(restTransitionsResponse4);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/four/transitions") && y.Method == Method.POST))).Returns(restResponse4);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestJira(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_jira_to_update_ticket_for_each_state_in_workflow() 
		{
			Card card = new Card() { Id = 1, ExternalSystemName = "Jira", ExternalCardID = "one" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/one") && y.Method == Method.GET)), Times.Exactly(4));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/one/transitions") && y.Method == Method.GET)), Times.Exactly(3));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/one/transitions") && y.Method == Method.POST)), Times.Exactly(3));
		}

		[Test]
		public void It_should_work_properly_with_spaces_between_states()
		{
			_mapping.LaneToStatesMap[2] = new List<string> { "active > resolved > closed" };
			Card card = new Card() { Id = 2, ExternalSystemName = "Jira", ExternalCardID = "two" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/two") && y.Method == Method.GET)), Times.Exactly(4));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/two/transitions") && y.Method == Method.GET)), Times.Exactly(3));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/two/transitions") && y.Method == Method.POST)), Times.Exactly(3));
		}

		[Test]
		public void It_should_not_call_jira_to_update_issue_for_states_it_is_in_or_past()
		{
			Card card3 = new Card() { Id = 3, ExternalSystemName = "Jira", ExternalCardID = "three" };
			Card card4 = new Card() { Id = 4, ExternalSystemName = "Jira", ExternalCardID = "four" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card3, _mapping.LaneToStatesMap[2], _mapping);
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card4, _mapping.LaneToStatesMap[2], _mapping);

			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/three") && y.Method == Method.GET)), Times.Exactly(3));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/three/transitions") && y.Method == Method.GET)), Times.Exactly(2));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/three/transitions") && y.Method == Method.POST)), Times.Exactly(2));

			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/four") && y.Method == Method.GET)), Times.Exactly(2));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/four/transitions") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/four/transitions") && y.Method == Method.POST)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_jira_to_update_issue_when_externalsystemname_does_not_match() 
		{
			Card card = new Card() { Id = 5, ExternalCardID = "five" };
			((TestJira)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);

			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("latest/issue/five") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("2/issue/five/transitions") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("issue/five/transitions") && y.Method == Method.POST)), Times.Never());
		}

	}

	public class When_syncronizing_with_target_system : JiraSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;
		private CardAddResult _testCardAddResult1;

		public class TestJira : Jira 
		{
			public TestJira(IBoardSubscriptionManager subscriptions,
									IConfigurationProvider<Configuration> configurationProvider,
									ILocalStorage<AppSettings> localStorage,
									ILeanKitClientFactory leanKitClientFactory,
									IRestClient restClient)
				: base(subscriptions, configurationProvider, localStorage, leanKitClientFactory, restClient) 
			{
				QueryDate = DateTime.UtcNow.AddMinutes(-1);
			}

			public void Syncronize(BoardMapping boardConfig) 
			{
				base.Synchronize(boardConfig);
			}
		}

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			_mapping = Test<BoardMapping>.Item;
			_testCardAddResult1 = Test<CardAddResult>.Item;
			_testCardAddResult1.CardId = 1;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.Query = "";
			_mapping.LaneToStatesMap.Add(1, new List<string> { "open" });
			TestConfig = Test<Configuration>.Item;
			TestConfig.PollingFrequency = 5000;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Jira.IssuesResponse>();

			var issue1 = new Jira.Issue()
			{
				Id = 1,
				Key = "one",
				Fields = new Jira.Fields()
					{
						Description = "Issue 1", 
						Status = new Jira.Status() { Description = "Open", Id = "1", Name = "Open"}, 
						Summary = "Issue 1"
					}
			};

			var issue2 = new Jira.Issue()
			{
				Id = 2,
				Key = "two",
				Fields = new Jira.Fields()
				{
					Description = "Issue 2",
					Status = new Jira.Status() { Description = "Open", Id = "1", Name = "Open" },
					Summary = "Issue 2"
				}
			};

			var issue3 = new Jira.Issue()
			{
				Id = 3,
				Key = "three",
				Fields = new Jira.Fields()
				{
					Description = "Issue 3",
					Status = new Jira.Status() { Description = "Open", Id = "1", Name = "Open" },
					Summary = "Issue 3"
				}
			};

			var issueResponse1 = new Jira.IssuesResponse()
				{
					Issues = new List<Jira.Issue>() {issue1}
				};

			var restResponse1 = new RestResponse()
			{
				Content = serializer.SerializeToString(issueResponse1),
				StatusCode = HttpStatusCode.OK
			};

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"1\"")) != null))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(1, It.IsAny<string>())).Returns((Card)null);
			MockLeanKitApi.Setup(x => x.AddCard(1, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"2\"")) != null))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(2, It.IsAny<string>())).Returns((Card)null);
			MockLeanKitApi.Setup(x => x.AddCard(2, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			var issueResponse3 = new Jira.IssuesResponse()
			{
				Issues = new List<Jira.Issue>() { issue1, issue2, issue3 }
			};

			var restResponse3 = new RestResponse()
			{
				Content = serializer.SerializeToString(issueResponse3),
				StatusCode = HttpStatusCode.OK
			};

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"3\"")) != null))).Returns(restResponse3);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(3, It.IsAny<string>())).Returns((Card)null);
			MockLeanKitApi.Setup(x => x.AddCard(3, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"4\"")) != null))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(4, It.IsAny<string>())).Returns(new Card() { Id = 4, ExternalSystemName = "Jira"});
			MockLeanKitApi.Setup(x => x.AddCard(4, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"5\"")) != null))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(5, It.IsAny<string>())).Returns(new Card() { Id = 4, ExternalSystemName = "Jirabus" });
			MockLeanKitApi.Setup(x => x.AddCard(5, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestJira(
				SubscriptionManager,
				ConfigurationProvider,
				LocalStorage,
				LeanKitClientFactory,
				RestClient);
		}

		[Test]
		public void It_should_call_jira_to_get_list_of_issues() 
		{
			_mapping.Identity.LeanKit = 1;
			_mapping.Identity.Target = "1";
			((TestJira)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"1\"")) != null)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(1, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(1));
		}

		[Test]
		public void It_should_call_leankit_once_to_create_card_if_there_is_one_issue() 
		{
			_mapping.Identity.LeanKit = 2;
			_mapping.Identity.Target = "2";
			((TestJira)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"2\"")) != null)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(2, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(1));
		}

		[Test]
		public void It_should_call_leankit_multiple_times_to_create_card_if_there_are_multiple_issues() 
		{
			_mapping.Identity.LeanKit = 3;
			_mapping.Identity.Target = "3";
			((TestJira)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"3\"")) != null)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(3, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(3));
		}

		[Test]
		public void It_should_not_call_leankit_to_create_card_if_card_with_externalid_already_exists() 
		{
			_mapping.Identity.LeanKit = 4;
			_mapping.Identity.Target = "4";
			((TestJira)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"4\"")) != null)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(4, It.IsAny<Card>(), It.IsAny<string>()), Times.Never());
		}

		[Test]
		public void It_should_call_leankit_to_create_card_if_card_with_externalid_exists_but_has_different_externalsystemname() 
		{
			_mapping.Identity.LeanKit = 5;
			_mapping.Identity.Target = "5";
			((TestJira)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("/search") && y.Method == Method.GET && y.Parameters.FirstOrDefault(z => z.Name == "jql" && z.Value.ToString().Contains("project=\"5\"")) != null)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(5, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(1));
		}
	}
}
