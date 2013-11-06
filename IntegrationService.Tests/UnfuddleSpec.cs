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
using IntegrationService.Targets.Unfuddle;
using IntegrationService.Util;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using Moq;
using NUnit.Framework;
using RestSharp;
using ServiceStack.Text;
using Should;
using Ploeh.SemanticComparison.Fluent;

namespace IntegrationService.Tests.UnFuddle
{
    public class UnfuddleSpec : IntegrationBaseSpec
    {
		protected Mock<IRestClient> MockRestClient;
		protected IRestClient RestClient;
	    protected Unfuddle TestItem;

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
            TestItem = new Unfuddle(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
        }

    }

    [TestFixture]
    public class When_starting_with_a_valid_configuration : UnfuddleSpec
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
    public class When_starting_with_an_invalid_configuration : UnfuddleSpec
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
    public class When_starting_with_valid_leankit_acount : UnfuddleSpec
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
	public class When_calculating_priority : UnfuddleSpec
	{
		protected override void OnStartFixture() 
		{
			TestConfig = Test<Configuration>.Item;
		}

		[Test]
		public void It_should_default_to_normal()
		{
			UnfuddleConversionExtensions.CalculateLeanKitPriority(null).ShouldEqual(1);
		}

		[Test]
		public void It_should_map_label_of_highest_to_critical()
		{
			UnfuddleConversionExtensions.CalculateLeanKitPriority(new Unfuddle.Ticket()
				{
				   Priority = 5
				}).ShouldEqual(3);
		}

		[Test]
		public void It_should_map_label_of_high_to_high() 
		{
			UnfuddleConversionExtensions.CalculateLeanKitPriority(new Unfuddle.Ticket()
			{
				Priority = 4
			}).ShouldEqual(2);
		}

		[Test]
		public void It_should_map_label_of_normal_to_normal() 
		{
			UnfuddleConversionExtensions.CalculateLeanKitPriority(new Unfuddle.Ticket()
			{
				Priority = 3
			}).ShouldEqual(1);
		}

		[Test]
		public void It_should_map_label_of_low_to_low() 
		{
			UnfuddleConversionExtensions.CalculateLeanKitPriority(new Unfuddle.Ticket()
			{
				Priority = 2
			}).ShouldEqual(0);
		}

		[Test]
		public void It_should_map_label_of_lowest_to_low() 
		{
			UnfuddleConversionExtensions.CalculateLeanKitPriority(new Unfuddle.Ticket()
			{
				Priority = 1
			}).ShouldEqual(0);
		}
	}

	[TestFixture]
	public class When_calculating_card_type : UnfuddleSpec 
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
			_mapping.Types = new List<WorkItemType>() { new WorkItemType() { LeanKit = "Willy", Target = "Roger"}};
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_return_default_card_type()
		{
			UnfuddleConversionExtensions.CalculateLeanKitCardType(_mapping, null).Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
			UnfuddleConversionExtensions.CalculateLeanKitCardType(_mapping, "").Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
		}
	}

	[TestFixture]
	public class When_calculating_assigned_user : UnfuddleSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture() 
		{
			_testBoard = Test<Board>.Item;
			int ctr = 0;
			foreach (var boardUser in _testBoard.BoardUsers) 
			{
				if (ctr == 0) {
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

			var serializer = new JsonSerializer<Unfuddle.Person>();

			var user1 = new Unfuddle.Person()
				{
					Email = "johnny@cash.com",
					First_Name = "Johnny",
					Id = 1,
					Last_Name = "Cash",
					Username = "jcash"
				};

			var user2 = new Unfuddle.Person()
			{
				Email = "willy@cash.com",
				First_Name = "Willy",
				Id = 2,
				Last_Name = "Cash",
				Username = "wcash"
			};

			var user3 = new Unfuddle.Person()
			{
				Email = "",
				First_Name = "",
				Id = 3,
				Last_Name = "",
				Username = ""
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(user1), StatusCode = HttpStatusCode.OK };
			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(user2), StatusCode = HttpStatusCode.OK };
			var restResponse3 = new RestResponse() { Content = serializer.SerializeToString(user3), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("people/1")))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("people/2")))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("people/3")))).Returns(restResponse3);
		}

		[Test]
		public void It_should_return_userid_on_matched_username() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 1
				}).ShouldEqual(101);
		}

		[Test]
		public void It_should_return_null_on_nonmatched_username() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 2 
				}).ShouldBeNull();
		}

		[Test]
		public void It_should_return_null_on_empty_username() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 3 
				}).ShouldBeNull();
		}


		[Test]
		public void It_should_return_userid_on_matched_email() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 1
				}).ShouldEqual(101);
		}

		[Test]
		public void It_should_return_null_on_nonmatched_email() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 2
				}).ShouldBeNull();
		}

		[Test]
		public void It_should_return_null_on_empty_email() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 3
				}).ShouldBeNull();
		}

		[Test]
		public void It_should_return_userid_on_matched_fullname() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 1
				}).ShouldEqual(101);
		}

		[Test]
		public void It_should_return_null_on_nonmatched_fullname() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 2
				}).ShouldBeNull();
		}

		[Test]
		public void It_should_return_null_on_empty_fullname() 
		{
			TestItem.CalculateAssignedUserId(_mapping.Identity.LeanKit,
				new Unfuddle.Ticket()
				{
					Assignee_Id = 3
				}).ShouldBeNull();
		}

	}

	public class When_updating_properties_of_target_item : UnfuddleSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		public class TestUnfuddle : Unfuddle 
		{
			public TestUnfuddle(IBoardSubscriptionManager subscriptions,
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

			var serializer = new JsonSerializer<Unfuddle.Ticket>();

			var ticket1 = new Unfuddle.Ticket()
			{
				Id = 1,
				Summary = "Ticket 1", 
				Description = "Ticket 1",
				Status = "Open"
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(ticket1), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.GET))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.PUT))).Returns(restResponse1);

			var ticket2 = new Unfuddle.Ticket()
			{
				Id = 2,
				Summary = "Ticket 2",
				Description = "Ticket 2",
				Status = "Open"
			};

			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(ticket2), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT))).Returns(restResponse2);

			var ticket3 = new Unfuddle.Ticket()
			{
				Id = 3,
				Summary = "Ticket 3",
				Description = "Ticket 3",
				Status = "Open"
			};

			var restResponse3 = new RestResponse() { Content = serializer.SerializeToString(ticket3), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.GET))).Returns(restResponse3);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.PUT))).Returns(restResponse3);

			var ticket4 = new Unfuddle.Ticket()
			{
				Id = 4,
				Summary = "Ticket 4",
				Description = "Ticket 4",
				Status = "Open"
			};

			var restResponse4 = new RestResponse() { Content = serializer.SerializeToString(ticket4), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.GET))).Returns(restResponse4);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.PUT))).Returns(restResponse4);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestUnfuddle(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_unfuddle_to_update_ticket_if_many_properties_change() 
		{
			Card card = new Card();
			card.ExternalCardID = "1";
			card.ExternalSystemName = "Unfuddle";
			card.Description = "Ticket 1 Description";
			card.Title = "Ticket 1 Title";

			((TestUnfuddle)TestItem).TestCardUpdated(card, new List<string>() { "Title", "Description" }, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.PUT)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_if_properties_do_not_change() 
		{
			Card card = new Card();
			card.ExternalCardID = "2";
			card.ExternalSystemName = "Unfuddle";
			card.Description = "Ticket 2";
			card.Title = "Ticket 2";

			((TestUnfuddle)TestItem).TestCardUpdated(card, new List<string>() { "Title", "Description" }, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_call_unfuddle_to_update_ticket_if_one_property_changes() 
		{
			Card card = new Card();
			card.ExternalCardID = "3";
			card.ExternalSystemName = "Unfuddle";
			card.Description = "Ticket 3";
			card.Title = "Ticket 3 Title";

			((TestUnfuddle)TestItem).TestCardUpdated(card, new List<string>() { "Title" }, _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.PUT)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_if_no_identified_properties_change() 
		{
			Card card = new Card();
			card.ExternalCardID = "4";
			card.ExternalSystemName = "Unfuddle";
			card.Description = "Ticket 4";
			card.Title = "Ticket 4";

			((TestUnfuddle)TestItem).TestCardUpdated(card, new List<string>(), _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_externalsystemname_does_not_match() 
		{
			Card card = new Card();
			card.ExternalCardID = "5";
			card.ExternalSystemName = "Unfuddlest";
			card.Description = "Ticket 5";
			card.Title = "Ticket 5";

			((TestUnfuddle)TestItem).TestCardUpdated(card, new List<string>(), _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/5") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/5") && y.Method == Method.PUT)), Times.Never());
		}	
	}

	public class When_updating_state_of_target_item : UnfuddleSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		public class TestUnfuddle : Unfuddle 
		{
			public TestUnfuddle(IBoardSubscriptionManager subscriptions,
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

			var serializer = new JsonSerializer<Unfuddle.Ticket>();

			var ticket1 = new Unfuddle.Ticket()
			{
				Id = 1,
				Status = "Open"
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(ticket1), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.GET))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.PUT))).Returns(restResponse1);

			var ticket2 = new Unfuddle.Ticket()
			{
				Id = 2,
				Status = "Closed"
			};

			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(ticket2), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT))).Returns(restResponse2);

			var errorSerializer = new JsonSerializer<Unfuddle.ErrorMessage>();
			var errorResponse = new RestResponse() { Content = errorSerializer.SerializeToString(new Unfuddle.ErrorMessage() { Message = "Error" }), StatusCode = HttpStatusCode.NotFound };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.GET))).Returns(errorResponse);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.PUT))).Returns(errorResponse);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestUnfuddle(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_unfuddle_to_update_ticket_if_ticket_state_is_not_end_state() 
		{
			Card card = new Card() { Id = 1, ExternalSystemName = "Unfuddle", ExternalCardID = "1" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.PUT)), Times.Exactly(1));
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_state_is_already_end_state() 
		{
			Card card = new Card() { Id = 2, ExternalSystemName = "Unfuddle", ExternalCardID = "2" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_if_card_does_not_have_external_id() 
		{
			Card card = new Card() { Id = 2, ExternalSystemName = "Unfuddle", ExternalCardID = "" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_if_unfuddle_does_not_have_matching_issue() 
		{
			Card card = new Card() { Id = 3, ExternalSystemName = "Unfuddle", ExternalCardID = "3" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.GET)), Times.Exactly(1));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.PUT)), Times.Never());
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_if_externalsystemname_does_not_match() 
		{
			Card card = new Card() { Id = 4, ExternalSystemName = "Unfuddlest", ExternalCardID = "4" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.PUT)), Times.Never());
		}
	}

	public class When_updating_state_of_target_item_through_workflow : UnfuddleSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		public class TestUnfuddle : Unfuddle 
		{
			public TestUnfuddle(IBoardSubscriptionManager subscriptions,
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
			_mapping.LaneToStatesMap.Add(2, new List<string> { "accepted>resolved>closed" });
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Unfuddle.Ticket>();

			var ticket1 = new Unfuddle.Ticket()
			{
				Id = 1,
				Status = "Open"
			};

			var restResponse1 = new RestResponse() { Content = serializer.SerializeToString(ticket1), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.GET))).Returns(restResponse1);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.PUT))).Returns(restResponse1);

			var ticket2 = new Unfuddle.Ticket()
			{
				Id = 2,
				Status = "Accepted"
			};

			var restResponse2 = new RestResponse() { Content = serializer.SerializeToString(ticket2), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET))).Returns(restResponse2);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT))).Returns(restResponse2);

			var ticket3 = new Unfuddle.Ticket()
			{
				Id = 3,
				Status = "Open"
			};

			var restResponse3 = new RestResponse() { Content = serializer.SerializeToString(ticket3), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.GET))).Returns(restResponse3);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.PUT))).Returns(restResponse3);

			var ticket4 = new Unfuddle.Ticket()
			{
				Id = 4,
				Status = "Resolved"
			};

			var restResponse4 = new RestResponse() { Content = serializer.SerializeToString(ticket4), StatusCode = HttpStatusCode.OK };

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.GET))).Returns(restResponse4);
			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.PUT))).Returns(restResponse4);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestUnfuddle(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory, RestClient);
		}

		[Test]
		public void It_should_call_unfuddle_to_update_ticket_for_each_state_of_workflow() 
		{
			Card card = new Card() { Id = 1, ExternalSystemName = "Unfuddle", ExternalCardID = "1" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.GET)), Times.Exactly(4));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/1") && y.Method == Method.PUT)), Times.Exactly(3));
		}

		[Test]
		public void It_should_work_properly_with_spaces_between_states() 
		{
			_mapping.LaneToStatesMap[2] = new List<string> { "accpted > resolved > closed" };
			Card card = new Card() { Id = 3, ExternalSystemName = "Unfuddle", ExternalCardID = "3" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.GET)), Times.Exactly(4));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/3") && y.Method == Method.PUT)), Times.Exactly(3));
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_for_states_it_is_in_or_past() 
		{
			Card card2 = new Card() { Id = 2, ExternalSystemName = "Unfuddle", ExternalCardID = "2" };
			Card card4 = new Card() { Id = 4, ExternalSystemName = "Unfuddle", ExternalCardID = "4" };

			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card2, _mapping.LaneToStatesMap[2], _mapping);
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card4, _mapping.LaneToStatesMap[2], _mapping);

			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.GET)), Times.Exactly(3));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/2") && y.Method == Method.PUT)), Times.Exactly(2));

			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.GET)), Times.Exactly(2));
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/4") && y.Method == Method.PUT)), Times.Exactly(1));	
		}

		[Test]
		public void It_should_not_call_unfuddle_to_update_ticket_if_externalstatename_does_not_match() 
		{
			Card card = new Card() { Id = 5, ExternalSystemName = "Unfuddlest", ExternalCardID = "5" };
			((TestUnfuddle)TestItem).TestUpdateStateOfExternalItem(card, _mapping.LaneToStatesMap[2], _mapping);

			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/5") && y.Method == Method.GET)), Times.Never());
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("tickets/5") && y.Method == Method.PUT)), Times.Never());
		}
	}

	public class When_syncronizing_with_target_system : UnfuddleSpec 
	{
		private Board _testBoard;
		private BoardMapping _mapping;
		private CardAddResult _testCardAddResult1;

		public class TestUnfuddle : Unfuddle 
		{
			public TestUnfuddle(IBoardSubscriptionManager subscriptions,
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
			_mapping.LaneToStatesMap.Add(1, new List<string> { "open" });
			TestConfig = Test<Configuration>.Item;
			TestConfig.PollingFrequency = 5000;
			TestConfig.Mappings = new List<BoardMapping> { _mapping };
		}

		protected override void OnArrange() 
		{
			base.OnArrange();

			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);

			var serializer = new JsonSerializer<Unfuddle.TicketsResponse>();

			var ticket1 = new Unfuddle.Ticket()
			{
				Id = 1,
				Status = "Open",
				Description = "Ticket 1",
				Summary = "Ticket 1"
			};

			var ticket2 = new Unfuddle.Ticket()
			{
				Id = 2,
				Status = "Open",
				Description = "Ticket 2",
				Summary = "Ticket 2"
			};

			var ticket3 = new Unfuddle.Ticket()
			{
				Id = 3,
				Status = "Open",
				Description = "Ticket 3",
				Summary = "Ticket 3"
			};

			var group1 = new Unfuddle.Group()
				{
					Tickets = new List<Unfuddle.Ticket>() {ticket1}
				};

			var unfuddleResponse1 = new Unfuddle.TicketsResponse()
				{
					Count = 1,
					Groups = new List<Unfuddle.Group>() {group1}
				};

			var restResponse1 = new RestResponse()
			{
				Content = serializer.SerializeToString(unfuddleResponse1),
				StatusCode = HttpStatusCode.OK
			};

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/1/ticket_reports") && y.Method == Method.GET))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(1, It.IsAny<string>())).Returns((Card)null);
			MockLeanKitApi.Setup(x => x.AddCard(1, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/2/ticket_reports") && y.Method == Method.GET))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(2, It.IsAny<string>())).Returns((Card)null);
			MockLeanKitApi.Setup(x => x.AddCard(2, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			var group3 = new Unfuddle.Group()
			{
				Tickets = new List<Unfuddle.Ticket>() { ticket1, ticket2, ticket3 }
			};

			var unfuddleResponse3 = new Unfuddle.TicketsResponse()
			{
				Count = 1,
				Groups = new List<Unfuddle.Group>() { group3 }
			};

			var restResponse3 = new RestResponse()
			{
				Content = serializer.SerializeToString(unfuddleResponse3),
				StatusCode = HttpStatusCode.OK
			};

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/3/ticket_reports") && y.Method == Method.GET))).Returns(restResponse3);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(3, It.IsAny<string>())).Returns((Card)null);
			MockLeanKitApi.Setup(x => x.AddCard(3, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/4/ticket_reports") && y.Method == Method.GET))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(4, It.IsAny<string>())).Returns(new Card() { Id = 4, ExternalSystemName = "Unfuddle" });
			MockLeanKitApi.Setup(x => x.AddCard(4, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);

			MockRestClient.Setup(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/5/ticket_reports") && y.Method == Method.GET))).Returns(restResponse1);
			MockLeanKitApi.Setup(x => x.GetCardByExternalId(5, It.IsAny<string>())).Returns(new Card() { Id = 4, ExternalSystemName = "Unfuddlest" });
			MockLeanKitApi.Setup(x => x.AddCard(5, It.IsAny<Card>(), It.IsAny<string>())).Returns(_testCardAddResult1);
		}

		protected override void OnStartTest() 
		{
			TestItem = new TestUnfuddle(
				SubscriptionManager,
				ConfigurationProvider,
				LocalStorage,
				LeanKitClientFactory,
				RestClient);
		}

		[Test]
		public void It_should_call_unfuddle_to_get_list_of_tickets() 
		{
			_mapping.Identity.LeanKit = 1;
			_mapping.Identity.Target = "1";
			((TestUnfuddle)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/1/ticket_reports") && y.Method == Method.GET)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(1, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(1));
		}

		[Test]
		public void It_should_call_leankit_once_to_create_card_if_there_is_one_ticket() 
		{
			_mapping.Identity.LeanKit = 2;
			_mapping.Identity.Target = "2";
			((TestUnfuddle)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/2/ticket_reports") && y.Method == Method.GET)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(2, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(1));
		}

		[Test]
		public void It_should_call_leankit_multiple_times_to_create_card_if_there_are_multiple_tickets() 
		{
			_mapping.Identity.LeanKit = 3;
			_mapping.Identity.Target = "3";
			((TestUnfuddle)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/3/ticket_reports") && y.Method == Method.GET)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(3, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(3));
		}

		[Test]
		public void It_should_not_call_leankit_to_create_card_if_card_with_externalid_already_exists() 
		{
			_mapping.Identity.LeanKit = 4;
			_mapping.Identity.Target = "4";
			((TestUnfuddle)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/4/ticket_reports") && y.Method == Method.GET)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(4, It.IsAny<Card>(), It.IsAny<string>()), Times.Never());
		}

		[Test]
		public void It_should_call_leankit_create_card_if_card_with_externalid_exists_but_has_different_externalsystemname() 
		{
			_mapping.Identity.LeanKit = 5;
			_mapping.Identity.Target = "5";
			((TestUnfuddle)TestItem).Syncronize(_mapping);
			MockRestClient.Verify(x => x.Execute(It.Is<RestRequest>(y => y.Resource.Contains("projects/5/ticket_reports") && y.Method == Method.GET)), Times.Exactly(1));
			MockLeanKitApi.Verify(x => x.AddCard(5, It.IsAny<Card>(), It.IsAny<string>()), Times.Exactly(1));
		}
	}
}