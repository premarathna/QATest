using Microsoft.Playwright;
using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace QATest;

public class Tests
{
    private string companyName;
    private string assetModel;
    private string assetStatus;
    private string user;
    private string location;
    private string newTagId;
    private IPage page;
    
    [SetUp]
    public async Task Setup()
    {
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = false });
        page = await browser.NewPageAsync();
        await page.GotoAsync("https://demo.snipeitapp.com/login");
    }
    
    [Test]
    public async Task Test1()
    {
        await login();
        await createAsset();
        await validateAssetCreation();
        await validateCreatedAsset();
    }
    
     private async Task login()
    {
        await page.FillAsync("#username", "admin");
        await page.FillAsync("#password", "password");
        await page.ClickAsync("button.btn.btn-primary.btn-block");
    }

    private async Task createAsset()
    {
        await page.WaitForSelectorAsync("a.dropdown-toggle", 
            new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
        await page.ClickAsync("a.dropdown-toggle");

        await page.ClickAsync("a[href='https://demo.snipeitapp.com/hardware/create']");

        await page.ClickAsync("#select2-company_select-container");
        var companyOption = page.Locator("#select2-company_select-results li.select2-results__option:first-child");
        companyName = await companyOption.Locator("div.clearfix > div:last-child").InnerTextAsync();
        await companyOption.ClickAsync();
        
        await page.ClickAsync("#select2-model_select_id-container");
        // Locate the first list option that contains `Apple Macbook Pro 13`.
        var macBookOption = page.Locator("#select2-model_select_id-results li:has-text('Apple macbook Pro 13')").First;
        assetModel = await macBookOption.InnerTextAsync();
        await macBookOption.ClickAsync();

        await page.ClickAsync("#select2-status_select_id-container");
        var statusOption = page.Locator("li.select2-results__option:has-text('Ready to Deploy')");
        assetStatus = await statusOption.InnerTextAsync();
        await statusOption.ClickAsync();

        // Select a random user from list
        await page.ClickAsync("#select2-assigned_user_select-container");
        var userOptions =  page.Locator("#select2-assigned_user_select-results li");
        
        int count = await userOptions.CountAsync();

        if (count > 0) {
            // Generate a random index within the range of available items
            Random random = new Random();
            int randomIndex = random.Next(0, count);

            // Get the random item and click it
            var randomUser = userOptions.Nth(randomIndex);
            user = await randomUser.Locator("div.clearfix > div:last-child").InnerTextAsync();
            await randomUser.ClickAsync();
        } else {
            Assert.Fail("No users found in list. User list cannot be empty.");
        }
        
        await page.ClickAsync("#select2-rtd_location_id_location_select-container");
        var locationOption = page.Locator("#select2-rtd_location_id_location_select-results li.select2-results__option:first-child");
        location = await locationOption.InnerTextAsync();
        await locationOption.ClickAsync();
        
        // Store Asset Tag to be verified later.
        newTagId = await page.Locator("#asset_tag").InputValueAsync();
        
        // Find and click save button.
        var saveButtons = page.Locator("button:has-text('Save')");
        await saveButtons.First.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task validateAssetCreation()
    {
        // Creating response task for the search Request that contain the newly created Asset Tag id.
        var responseTask = page.WaitForResponseAsync(response =>
            response.Url.Contains($"https://demo.snipeitapp.com/api/v1/hardware?status=&order_number=&company_id=&status_id=&search={newTagId}&sort=name&order=asc&offset=0&limit=20") && response.Status == 200);
        
        // Searching for the asset with the Asset Tag
        // Search is required as the created asset is not guaranteed to be on the first page of the list.
        var searchInput = page.Locator("input.form-control.search-input");
        await searchInput.FillAsync($"{newTagId}");
        await searchInput.PressAsync("Enter");

        // Waiting for the Search request to finish with a 200 status.
        await responseTask;

        var rows = page.Locator("table tbody tr");
        int rowCount = await rows.CountAsync();

        bool assetFound = false;
        // To store the Anchor of the asset to be used for navigation.
        ILocator newAssetAnchor = null;

        if (rowCount > 0)
        {
            // Iterate through each row
            for (int i = 0; i < rowCount; i++) {
                var row = rows.Nth(i);

                // Access the 4th column(Asset Tag)'s <a> tag.
                var assetAnchor = row.Locator("td:nth-child(4) a");

                // Get the asset tag from the <a> tag.
                string assetTag = await assetAnchor.InnerTextAsync();

                // Check if the asset tags match.
                if (assetTag.Equals(newTagId)) {
                    assetFound = true;
                    newAssetAnchor = assetAnchor;
                }
            }
        } else {
            Assert.Fail("No rows found in the table.");
        }

        Assert.True(assetFound, "The item created was not found in list.");

        // Opening the found asset.
        await newAssetAnchor.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    private async Task validateCreatedAsset()
    {
        var assetTag = await page.Locator("span.js-copy-assettag").InnerTextAsync();
        //var statusRow = page.Locator("div.row-new-striped div").Nth(1);
        //var status = statusRow.Locator("div.col-md-9").InnerTextAsync();
        
        Assert.AreEqual(newTagId, assetTag, "The asset tag does not match.");
        //Assert.AreEqual(assetStatus, status, "The asset tag does not match.");
        
    }
    
    [TearDown]
    public void Cleanup()
    {
        // Clean up or close the browser if needed
        page.CloseAsync().Wait();
    }
}




