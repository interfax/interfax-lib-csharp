﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using NUnit.Framework;
using Scotch;
using System.Threading.Tasks;
using InterFAX.Api.Test.Integration.extensions;

namespace InterFAX.Api.Test.Integration
{
    [TestFixture]
    public class OutboundTests
    {
        private FaxClient _interfax;
        private readonly string _testPath;

		private String _faxNumber = TestingConfig.faxNumber;
		private int _outboundFaxID = TestingConfig.outboundFaxID;


		public OutboundTests()
        {
            _testPath = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;
        }

        [SetUp]
        public void Setup()
        {
			var httpClient = HttpClients.NewHttpClient(_testPath + TestingConfig.scotchCassettePath, TestingConfig.scotchMode);
			_interfax = new FaxClient(TestingConfig.username, TestingConfig.password, httpClient);
        }


        [Test]
		[IgnoreMocked]
        public void can_get_outbound_fax_list()
        {
            var list = _interfax.Outbound.GetList().Result;
            Assert.IsTrue(list.Any());
        }


        [Test]
		[IgnoreMocked]
        public void can_get_outbound_fax_list_with_listoptions()
        {
            // not testing the results, except that they should be a list of 
            // whether the REST api is working correctly or not isn't part of these tests.

            var list = _interfax.Outbound.GetList(new Outbound.ListOptions
            {
                LastId = 0,
                Limit = 2,
                SortOrder = ListSortOrder.Ascending
            }).Result;
            Assert.IsTrue(list.Any());
        }

        [Test]
		[IgnoreMocked]

		public void can_stream_fax_image_to_file()
        {
            var filename = $"{Guid.NewGuid().ToString()}.tiff";
			var filepath = _testPath + '/' + filename;

			// Fax a document
			var faxDocument = _interfax.Documents.BuildFaxDocument(Path.Combine(_testPath, "test.pdf"));
            var faxId = _interfax.Outbound.SendFax(faxDocument, new SendOptions { FaxNumber = _faxNumber }).Result;
            Assert.True(faxId > 0);

            // Have to pause for a moment as the image isn't immediately available
            System.Threading.Thread.Sleep(5000);

            using (var imageStream = _interfax.Outbound.GetFaxImageStream(faxId).Result)
            {
                using (var fileStream = File.Create(filepath))
                {
                    Utils.CopyStream(imageStream, fileStream);
                }
            }

            Assert.IsTrue(File.Exists(filepath));
            Assert.IsTrue(new FileInfo(filepath).Length > 0);
            File.Delete(filepath);
        }

        [Test]
        [Ignore("The fax api appears to have changed since this was last run - cancelling a sent fax returns OK.")]
        public void cancelling_already_sent_fax_builds_error_response()
        {
            // Fax the document
            var faxDocument = _interfax.Documents.BuildFaxDocument(Path.Combine(_testPath, "test.pdf"));
            var faxId = _interfax.Outbound.SendFax(faxDocument, new SendOptions { FaxNumber = _faxNumber }).Result;
            Assert.True(faxId > 0);

            var exception = Assert.Throws<AggregateException>(() =>
            {
                var result = _interfax.Outbound.CancelFax(faxId).Result;
            });

            var apiException = exception.InnerExceptions[0] as ApiException;
            Assert.NotNull(apiException);

            var error = apiException.Error;
            Assert.AreEqual(HttpStatusCode.Conflict, apiException.StatusCode);
            Assert.AreEqual(-162, error.Code);
            Assert.AreEqual("Transaction is in a wrong status for this operation", error.Message);
            Assert.AreEqual("Transaction ID 661900007 has already completed", error.MoreInfo);
        }

        [Test]
        public void can_cancel_fax()
        {
			int messageId = _outboundFaxID;

            var exception = Assert.Throws<AggregateException>(() =>
            {
                var response = _interfax.Outbound.CancelFax(messageId).Result;
            });
            Assert.NotNull(exception);
        }

        [Test]
		[IgnoreMocked]
        public void can_hide_fax()
        {
            var faxDocument = _interfax.Documents.BuildFaxDocument(Path.Combine(_testPath, "test.pdf"));
            var faxId = _interfax.Outbound.SendFax(faxDocument, new SendOptions
            {
                FaxNumber = _faxNumber,

            }).Result;

            // verify it shows up in the list
            var faxes = _interfax.Outbound.SearchFaxes(new SearchOptions { Ids = new [] {faxId}}).Result;
            Assert.AreEqual(1, faxes.Count());

            // hide the fax
            var response = _interfax.Outbound.HideFax(faxId).Result;

            // search again
            faxes = _interfax.Outbound.SearchFaxes(new SearchOptions { Ids = new[] { faxId } }).Result;
            Assert.AreEqual(0, faxes.Count());
        }

        [Test]
        public void can_get_completed_fax()
        {
            var faxDocument = _interfax.Documents.BuildFaxDocument(Path.Combine(_testPath, "test.pdf"));
            var faxId = _interfax.Outbound.SendFax(faxDocument, new SendOptions
            {
                FaxNumber = _faxNumber,

            }).Result;

            // get the completed fax
            var response = _interfax.Outbound.GetCompleted(faxId).Result;
            if(TestingConfig.scotchMode != ScotchMode.Replaying) Assert.NotNull(response);
        }

        [Test]
        public void can_send_fax()
        {
            var faxDocument = _interfax.Documents.BuildFaxDocument(Path.Combine(_testPath, "test.pdf"));
			var response = _interfax.Outbound.SendFax(faxDocument, new SendOptions
			{
				FaxNumber = _faxNumber,

			}).Result;
        }

        [Test]
        public void can_send_multiple_inline_faxes()
        {
            var path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;

            var faxDocuments = new List<IFaxDocument>
            {
                _interfax.Documents.BuildFaxDocument(Path.Combine(path, "test.pdf")),
                _interfax.Documents.BuildFaxDocument(Path.Combine(path, "test.html")),
                _interfax.Documents.BuildFaxDocument(Path.Combine(path, "test.txt"))
            };

            var response = _interfax.Outbound.SendFax(faxDocuments, new SendOptions
            {
                FaxNumber = _faxNumber,

            }).Result;
        }

        [Test]
        public void can_send_multiple_mixed_faxes()
        {
            var path = new Uri(Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase)).LocalPath;

            var faxDocuments = new List<IFaxDocument>
            {
                _interfax.Documents.BuildFaxDocument(Path.Combine(path, "test.pdf")),
                _interfax.Documents.BuildFaxDocument(new Uri("https://en.wikipedia.org/wiki/Representational_state_transfer")),
            };

            var response = _interfax.Outbound.SendFax(faxDocuments, new SendOptions
            {
                FaxNumber = _faxNumber,

            }).Result;
        }
    }
}
