﻿using System;
using System.Collections.Generic;
using System.Security.Claims;
using Fabric.Authorization.API.Constants;
using Nancy;
using Nancy.Testing;
using Xunit;

namespace Fabric.Authorization.IntegrationTests.Modules
{
    [Collection("InMemoryTests")]
    public class PermissionsTests : IClassFixture<IntegrationTestsFixture>
    {
        private readonly Browser _browser;
        private readonly string _securableItem;

        public PermissionsTests(IntegrationTestsFixture fixture, bool useInMemoryDb = true)
        {
            _securableItem = "permissionprincipal" + Guid.NewGuid();
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new List<Claim>
            {
                new Claim(Claims.Scope, Scopes.ManageClientsScope),
                new Claim(Claims.Scope, Scopes.ReadScope),
                new Claim(Claims.Scope, Scopes.WriteScope),
                new Claim(Claims.ClientId, _securableItem)
            }, _securableItem));

            _browser = fixture.GetBrowser(principal, useInMemoryDb);
            fixture.CreateClient(_browser, _securableItem);
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("986201B4-F593-44CA-882E-ABD32DA26DF9")]
        [InlineData("21967D2D-6B95-4461-8B2D-6398303262B0")]
        public void TestGetPermission_Fail(string permission)
        {
            var get = _browser.Get($"/permissions/app/{_securableItem}/{permission}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.OK, get.StatusCode); //TODO: Should be OK or NotFound?
            Assert.True(!get.Body.AsString().Contains(permission));
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("E795EFB0-94D1-40CD-A317-BCDE60C9C30A")]
        [InlineData("0E2C3C9E-D593-4941-9750-7EF3B11C4BFE")]
        public void TestAddNewPermission_Success(string permission)
        {
            var postResponse = _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission
                });
            }).Result;

            // Get by name
            var getResponse = _browser.Get($"/permissions/app/{_securableItem}/{permission}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.Contains(permission, getResponse.Body.AsString());
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("B9AEAA5A-C079-4BF8-84AC-44B8EF79B4D1")]
        [InlineData("3E613C1B-D6A6-491B-8927-91566B5EAF2C")]
        public void TestGetPermission_Success(string permission)
        {
            var postResponse = _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission
                });
            }).Result;

            // Get by name
            var getResponse = _browser.Get($"/permissions/app/{_securableItem}/{permission}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.Contains(permission, getResponse.Body.AsString());

            // Get by secitem
            getResponse = _browser.Get($"/permissions/app/{_securableItem}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            Assert.Contains(permission, getResponse.Body.AsString());
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("AE01ADF0-B88F-46A5-BBA0-3C697B0386C3")]
        [InlineData("807D63CD-4AB3-4756-ADED-3EA4B6C960FE")]
        public void TestGetPermissionForSecItem_Success(string permission)
        {
            var postResponse = _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission + "_1"
                });
            }).Result;

            postResponse = _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission + "_2"
                });
            }).Result;

            // Get by secitem
            var getResponse = _browser.Get($"/permissions/app/{_securableItem}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

            Assert.Contains(permission + "_1", getResponse.Body.AsString());
            Assert.Contains(permission + "_2", getResponse.Body.AsString());
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("CA75CDD1-51F9-40C3-94C7-6DE9EA87FEEC")]
        [InlineData("3A8AA05D-B2E8-4E45-94CA-E8E485356999")]
        public void TestAddNewPermission_Fail(string permission)
        {
            var validPostResponse = _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission
                });
            }).Result;

            Assert.Equal(HttpStatusCode.Created, validPostResponse.StatusCode);

            // Repeat
            var postResponse = _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission
                });
            }).Result;

            Assert.Equal(HttpStatusCode.Conflict, postResponse.StatusCode);
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("2D8AA649-EEE3-4B96-8A9C-DB24936CFBEF")]
        [InlineData("537B1742-4D2B-42F4-A29B-73DA3E074E93")]
        public void TestDeletePermission_Success(string permission)
        {
            var id = Guid.NewGuid();

            _browser.Post("/permissions", with =>
            {
                with.HttpRequest();
                with.JsonBody(new
                {
                    Id = id.ToString(),
                    Grain = "app",
                    SecurableItem = _securableItem,
                    Name = permission
                });
            }).Wait();

            var delete = _browser.Delete($"/permissions/{id}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }

        [Theory]
        [IntegrationTestsFixture.DisplayTestMethodName]
        [InlineData("18F06565-AA9E-4315-AF27-CEFC165B20FA")]
        [InlineData("18F06565-AA9E-4315-AF27-CEFC165B20FB")]
        public void TestDeletePermission_Fail(string permission)
        {
            var delete = _browser.Delete($"/permissions/{permission}", with =>
            {
                with.HttpRequest();
            }).Result;

            Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        }
    }
}