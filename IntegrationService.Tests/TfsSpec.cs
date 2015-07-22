//------------------------------------------------------------------------------
// <copyright company="LeanKit Inc.">
//     Copyright (c) LeanKit Inc.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using IntegrationService.Targets.TFS;
using LeanKit.API.Client.Library;
using LeanKit.API.Client.Library.TransferObjects;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Moq;
using NUnit.Framework;
using Should;

namespace IntegrationService.Tests.TFS
{
	public class TfsSpec : IntegrationBaseSpec
	{
		protected Mock<ICredentials> MockCredentials;
		protected ICredentials Credentials;
		protected BasicAuthCredential BasicAuthCredential;
		protected TfsClientCredentials TfsClientCredentials;
		protected Mock<TfsTeamProjectCollection> MockProjectCollection;
		protected TfsTeamProjectCollection ProjectCollection;
		protected TswaClientHyperlinkService ProjectHyperlinkService;
		protected WorkItemStore WorkItemStore;
		protected List<Microsoft.TeamFoundation.Server.Identity> TFSUsers;
		protected new Tfs TestItem;

		protected override void OnCreateMockObjects()
		{
			base.OnCreateMockObjects();
			MockCredentials = new Mock<ICredentials>();
			Credentials = MockCredentials.Object;
			BasicAuthCredential = new BasicAuthCredential(Credentials);
			TfsClientCredentials = new TfsClientCredentials(BasicAuthCredential);
			TfsClientCredentials.AllowInteractive = false;

			MockProjectCollection = new Mock<TfsTeamProjectCollection>(new Uri("http://localhost"), Credentials);
			ProjectCollection = MockProjectCollection.Object;

			ProjectHyperlinkService = null;
			WorkItemStore = null;
			TFSUsers = new List<Microsoft.TeamFoundation.Server.Identity>();
		}

		protected override void OnArrange()
		{
			MockConfigurationProvider.Setup(x => x.GetConfiguration()).Returns(TestConfig);
			MockLeanKitClientFactory.Setup(x => x.Create(It.IsAny<ILeanKitAccountAuth>())).Returns(LeanKitApi);
		}

		protected override void OnStartTest()
		{
			TestItem = new Tfs(SubscriptionManager, ConfigurationProvider, LocalStorage, LeanKitClientFactory,
				Credentials, BasicAuthCredential, TfsClientCredentials, ProjectCollection, ProjectHyperlinkService, WorkItemStore,
				TFSUsers);
		}
	}

	[TestFixture]
	public class When_calculating_priority : SpecBase
	{
		[Test]
		public void It_should_default_to_normal()
		{
			ConversionExtensions.CalculateLeanKitPriority(null).ShouldEqual(1);
			ConversionExtensions.CalculateLeanKitPriority("").ShouldEqual(1);
		}

		[Test]
		public void It_should_map_priority_of_4_to_low()
		{
			ConversionExtensions.CalculateLeanKitPriority("4").ShouldEqual(0);
		}

		[Test]
		public void It_should_map_priority_of_3_to_normal()
		{
			ConversionExtensions.CalculateLeanKitPriority("3").ShouldEqual(1);
		}

		[Test]
		public void It_should_map_priority_of_2_to_high()
		{
			ConversionExtensions.CalculateLeanKitPriority("2").ShouldEqual(2);
		}

		[Test]
		public void It_should_map_priority_of_1_to_critical()
		{
			ConversionExtensions.CalculateLeanKitPriority("1").ShouldEqual(3);
		}

		[Test]
		public void It_should_not_blow_up_when_it_gets_junk_input()
		{
			ConversionExtensions.CalculateLeanKitPriority("Roscoe").ShouldEqual(1);
			ConversionExtensions.CalculateLeanKitPriority("40").ShouldEqual(1);
			ConversionExtensions.CalculateLeanKitPriority("0").ShouldEqual(1);
			ConversionExtensions.CalculateLeanKitPriority("6").ShouldEqual(1);
		}
	}

	[TestFixture]
	public class When_calculating_card_type : TfsSpec
	{
		private Board _testBoard;
		private BoardMapping _mapping;

		protected override void OnStartFixture()
		{
			_testBoard = Test<Board>.Item;
			foreach (var cardType in _testBoard.CardTypes)
				cardType.IsDefault = false;
			_testBoard.CardTypes.Add(new CardType() {Id = 999, Name = "Willy", IsDefault = false});
			_testBoard.CardTypes.Last().IsDefault = true;
			_mapping = Test<BoardMapping>.Item;
			_mapping.Identity.LeanKit = _testBoard.Id;
			_mapping.Types = new List<WorkItemType>() {new WorkItemType() {LeanKit = "Willy", Target = "Roger"}};
			TestConfig = Test<Configuration>.Item;
			TestConfig.Mappings = new List<BoardMapping> {_mapping};
		}

		protected override void OnArrange()
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_return_default_card_type_if_issue_has_no_priority()
		{
			ConversionExtensions.CalculateLeanKitCardType(_mapping, null).Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
			ConversionExtensions.CalculateLeanKitCardType(_mapping, "").Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
		}

		[Test]
		public void It_should_return_default_card_type_if_issue_has_no_matching_priority()
		{
			ConversionExtensions.CalculateLeanKitCardType(_mapping, "Bob").Id.ShouldEqual(_testBoard.CardTypes.Last().Id);
		}

		[Test]
		public void It_should_return_implicit_card_type_if_issue_has_matching_priority()
		{
			ConversionExtensions.CalculateLeanKitCardType(_mapping, "Willy")
				.Id.ShouldEqual(_testBoard.CardTypes.FirstOrDefault(x => x.Name == "Willy").Id);
		}

		[Test]
		public void It_should_return_mapped_card_type_if_issue_has_matching_label()
		{
			ConversionExtensions.CalculateLeanKitCardType(_mapping, "Roger")
				.Id.ShouldEqual(_testBoard.CardTypes.FirstOrDefault(x => x.Name == "Willy").Id);
		}
	}

	[TestFixture]
	public class When_calculating_assigned_user : TfsSpec
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
			TestConfig.Mappings = new List<BoardMapping> {_mapping};
		}

		protected override void OnArrange()
		{
			base.OnArrange();
			MockLeanKitApi.Setup(x => x.GetBoard(It.IsAny<long>())).Returns(_testBoard);
		}

		[Test]
		public void It_should_return_null_on_empty_username()
		{
			TestItem.CalculateAssignedUserId(_testBoard.Id, null).ShouldBeNull();
			TestItem.CalculateAssignedUserId(_testBoard.Id, "").ShouldBeNull();
		}

//		[Test]
//		public void It_should_return_userid_on_matched_username()
//		{
//		}
//
//		[Test]
//		public void It_should_return_null_on_nonmatched_username()
//		{
//		}
//
//		[Test]
//		public void It_should_return_userid_on_matched_email()
//		{
//		}
//
//		[Test]
//		public void It_should_return_null_on_nonmatched_email()
//		{
//		}
//
//		[Test]
//		public void It_should_return_userid_on_matched_fullname()
//		{
//		}
//
//		[Test]
//		public void It_should_return_null_on_nonmatched_fullname()
//		{
//
//		}
	}
}
