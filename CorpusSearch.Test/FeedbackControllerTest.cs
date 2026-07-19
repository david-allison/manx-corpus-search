using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CorpusSearch.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace CorpusSearch.Test;

[TestFixture]
public class FeedbackControllerTest
{
    private StubHandler handler = null!;

    [SetUp]
    public void SetUp() => handler = new StubHandler();

    private FeedbackController Controller(string? appsScriptUrl = "https://script.example/exec") =>
        new(new StubHttpClientFactory(handler), new FeedbackConfig { AppsScriptUrl = appsScriptUrl })
        {
            // the controller reads HttpContext.RequestAborted
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static FeedbackController.FeedbackRequest Request(
        string comments = "the plural is wrong",
        string? name = null,
        string? website = null) => new()
    {
        Name = name,
        Comments = comments,
        Dictionary = "Cregeen",
        Headword = "aa",
        Website = website,
    };

    [Test]
    public async Task UnconfiguredIsDark()
    {
        var result = await Controller(appsScriptUrl: null).Post(Request());
        Assert.That(result, Is.InstanceOf<NotFoundResult>());
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task BlankCommentsAreRejected()
    {
        var result = await Controller().Post(Request(comments: "   "));
        Assert.That(result, Is.InstanceOf<BadRequestResult>());
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task HoneypotClaimsSuccessWithoutRelaying()
    {
        var result = await Controller().Post(Request(website: "https://spam.example"));
        Assert.That(result, Is.InstanceOf<NoContentResult>());
        Assert.That(handler.Requests, Is.Empty);
    }

    [Test]
    public async Task RelaySuccess()
    {
        handler.Respond(HttpStatusCode.OK, """{"ok":true}""");
        var result = await Controller().Post(Request(name: "Juan"));

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        Assert.That(handler.Requests, Has.Count.EqualTo(1));
        Assert.That(handler.Bodies[0], Does.Contain("\"comments\":\"the plural is wrong\""));
        Assert.That(handler.Bodies[0], Does.Contain("\"name\":\"Juan\""));
        Assert.That(handler.Bodies[0], Does.Contain("\"dictionary\":\"Cregeen\""));
        Assert.That(handler.Bodies[0], Does.Contain("\"headword\":\"aa\""));
        // the honeypot marks bots on the way in; it is not the sheet's business
        Assert.That(handler.Bodies[0], Does.Not.Contain("website"));
    }

    /// <remarks>An Apps Script that crashed still answers 200, with an HTML
    /// error page: only the script's own marker means the row was written</remarks>
    [Test]
    public async Task OkWithoutMarkerIsAFailure()
    {
        handler.Respond(HttpStatusCode.OK, "<html>TypeError: ...</html>");
        var result = await Controller().Post(Request());
        Assert.That(result, Is.InstanceOf<StatusCodeResult>());
        Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(502));
    }

    [Test]
    public async Task NetworkFailureIsABadGateway()
    {
        handler.Throw(new HttpRequestException("no route to host"));
        var result = await Controller().Post(Request());
        Assert.That(result, Is.InstanceOf<StatusCodeResult>());
        Assert.That(((StatusCodeResult)result).StatusCode, Is.EqualTo(502));
    }

    [Test]
    public async Task OverLongFieldsAreTruncatedNotRejected()
    {
        handler.Respond(HttpStatusCode.OK, """{"ok":true}""");
        var result = await Controller().Post(Request(comments: new string('x', 3000)));

        Assert.That(result, Is.InstanceOf<NoContentResult>());
        Assert.That(handler.Bodies[0], Does.Contain(new string('x', 2000)));
        Assert.That(handler.Bodies[0], Does.Not.Contain(new string('x', 2001)));
    }

    private class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> Bodies { get; } = [];
        private HttpResponseMessage response = new(HttpStatusCode.OK);
        private Exception? exception;

        public void Respond(HttpStatusCode status, string body) =>
            response = new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

        public void Throw(Exception e) => exception = e;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            // read now: the controller disposes the request before assertions run
            Bodies.Add(request.Content == null
                ? ""
                : await request.Content.ReadAsStringAsync(cancellationToken));
            if (exception != null)
            {
                throw exception;
            }
            return response;
        }
    }

    private class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
