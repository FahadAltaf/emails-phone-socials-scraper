using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PuppeteerSharp;

class Program
{
    static async Task Main(string[] args)
    {
        List<string> websites = new List<string>
        {
            "https://backyardsocial.com/",
            "https://www.blackradishtlh.com/",
            "https://tradervicspalm.com/",
            "https://www.bistrology.restaurant/",
            "https://frontporchsocialcc.com/",
            "https://throwsomeshadeorl.com/"
        };

        var results = await ProcessWebsitesAsync(websites);

        foreach (var (website, info) in results)
        {
            Console.WriteLine($"Website: {website}");
            Console.WriteLine($"Emails: {string.Join(", ", info.Emails)}");
            Console.WriteLine($"Social Media: {string.Join(", ", info.SocialMedia)}");
            Console.WriteLine($"Phone Numbers: {string.Join(", ", info.PhoneNumbers)}");
            Console.WriteLine();
        }
    }

    static async Task<Dictionary<string, (HashSet<string> Emails, HashSet<string> SocialMedia, HashSet<string> PhoneNumbers)>> ProcessWebsitesAsync(List<string> websites)
    {
        var results = new Dictionary<string, (HashSet<string> Emails, HashSet<string> SocialMedia, HashSet<string> PhoneNumbers)>();

        var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync();

        var launchOptions = new LaunchOptions { Headless = false };
        launchOptions.DefaultViewport = new ViewPortOptions { Width = 1920, Height = 1080 };
        await using var browser = await Puppeteer.LaunchAsync(launchOptions);

        foreach (var website in websites)
        {
            Console.WriteLine($"Processing: {website}");
            var (emails, socialMedia, phoneNumbers) = await ExtractInfoFromWebsiteAsync(browser, website);
            results[website] = (emails, socialMedia, phoneNumbers);
            await Task.Delay(2000); // Be polite, don't overwhelm servers
        }

        return results;
    }

    static async Task<(HashSet<string> Emails, HashSet<string> SocialMedia, HashSet<string> PhoneNumbers)> ExtractInfoFromWebsiteAsync(IBrowser browser, string baseUrl)
    {
        var emails = new HashSet<string>();
        var socialMedia = new HashSet<string>();
        var phoneNumbers = new HashSet<string>();

        await using var page = await browser.NewPageAsync();
        await page.GoToAsync(baseUrl);
        await Task.Delay(5000);
        var menuLinks = await ExtractMenuLinksAsync(page, baseUrl);

        for (int i = 0; i < menuLinks.Count; i += 5)
        {
            var batchLinks = menuLinks.GetRange(i, Math.Min(5, menuLinks.Count - i));
            var batchResults = await Task.WhenAll(batchLinks.Select(link => ExtractInfoFromPageAsync(browser, link)));

            foreach (var (pageEmails, pageSocialMedia, pagePhoneNumbers) in batchResults)
            {
                emails.UnionWith(pageEmails);
                socialMedia.UnionWith(pageSocialMedia);
                phoneNumbers.UnionWith(pagePhoneNumbers);
            }

            await Task.Delay(1000); // Be polite, don't overwhelm servers
        }

        return (emails, socialMedia, phoneNumbers);
    }

    static async Task<List<string>> ExtractMenuLinksAsync(IPage page, string baseUrl)
    {
        var menuLinks = await page.EvaluateFunctionAsync<List<string>>(@"(baseUrl) => {
            const menuElements = document.querySelectorAll('a');
            return Array.from(menuElements)
                .map(a => a.href)
                .filter(href => href.startsWith(baseUrl) || href.startsWith('/'))
                .filter(href => href.includes(baseUrl) || href.startsWith('/'))
                .map(href => href.startsWith('/') ? new URL(href, baseUrl).href : href);
        }", baseUrl);

        return menuLinks.Distinct().ToList();
    }

    static async Task<(HashSet<string> Emails, HashSet<string> SocialMedia, HashSet<string> PhoneNumbers)> ExtractInfoFromPageAsync(IBrowser browser, string url)
    {
        var emails = new HashSet<string>();
        var socialMedia = new HashSet<string>();
        var phoneNumbers = new HashSet<string>();

        try
        {
            await using var page = await browser.NewPageAsync();
            await page.GoToAsync(url);
            await Task.Delay(5000);
            var content = await page.GetContentAsync();

            // Extract emails
            string emailRegex = @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b";
            var matches = Regex.Matches(content, emailRegex, RegexOptions.IgnoreCase);
            emails.UnionWith(matches.Cast<Match>().Select(m => m.Value).Where(IsValidEmail));

            // Extract social media links
            var socialMediaPatterns = new Dictionary<string, string>
            {
                { "Facebook", @"facebook\.com" },
                { "Twitter", @"twitter\.com" },
                { "LinkedIn", @"linkedin\.com" },
                { "Instagram", @"instagram\.com" },
                // Add more social media patterns as needed
            };

            // Extract phone numbers
            string phoneRegex = @"\b(?:\+?1[-.\s]?)?(?:\(\d{3}\)|\d{3})[-.\s]?\d{3}[-.\s]?\d{4}\b";
            var phoneMatches = Regex.Matches(content, phoneRegex);
            phoneNumbers.UnionWith(phoneMatches.Cast<Match>().Select(m => m.Value));

            // Extract social media links
            var links = await page.EvaluateExpressionAsync<string[]>(
                "Array.from(document.querySelectorAll('a')).map(a => a.href)");

            foreach (var href in links)
            {
                foreach (var (platform, pattern) in socialMediaPatterns)
                {
                    if (Regex.IsMatch(href, pattern))
                    {
                        socialMedia.Add($"{platform}: {href}");
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error processing {url}: {e.Message}");
        }

        return (emails, socialMedia, phoneNumbers);
    }

    static bool IsValidEmail(string email)
    {
        string emailRegex = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(email, emailRegex);
    }
}