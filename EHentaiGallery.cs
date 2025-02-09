﻿using EHentaiAPI;
using EHentaiAPI.Client;
using EHentaiAPI.Client.Data;
using EHentaiAPI.ExtendFunction;
using EHentaiAPI.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading.Tasks;
using Wbooru;
using Wbooru.Galleries;
using Wbooru.Galleries.SupportFeatures;
using Wbooru.Models.Gallery;
using Wbooru.Settings;
using Wbooru.UI.Pages;
using WbooruPlugin.EHentai.UI;
using WbooruPlugin.EHentai.UI.Pages;
using WbooruPlugin.EHentai.Utils;

namespace WbooruPlugin.EHentai
{
    [Export(typeof(Gallery))]
    public class EHentaiGallery : Gallery, ICustomDetailImagePage, IGalleryAccount, IGallerySearchImage
    {
        public const string GalleryNameConst = "EHentai";

        private EhClient client;

        public override string GalleryName => GalleryNameConst;

        public bool IsLoggined => client.Cookies.GetCookies(new Uri(client.EhUrl.GetHost()))?.Any(x => x.Name.Equals("ipb_pass_hash", System.StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("ipb_member_id", System.StringComparison.InvariantCultureIgnoreCase)) ?? false;

        public CustomLoginPage CustomLoginPage => new DefaultLoginPage(this);

        public EHentaiGallery()
        {
            client = new EhClient();
            client.Settings.SharedPreferences = Setting<EhentaiSetting>.Current;

            Request.RequestFactory = url => new EHentaiRequest(url);

            //暂时表站
            client.Settings.GallerySite = Settings.GallerySites.SITE_E;

            //强制钦定一下列表样式
            client.Cookies.Add(new System.Net.Cookie("sl", "dm_1", "/", "e-hentai.org"));
            client.Cookies.Add(new System.Net.Cookie("sl", "dm_1", "/", "exhentai.org"));
        }

        public override Task<GalleryItem> GetImage(string id)
        {
            if (!IdConverter.TryConvertBackEhentai(id, out var gid, out var token))
            {
                return default;
            }

            var url = client.EhUrl.GetGalleryDetailUrl(gid, token);
            var detail = client.GetGalleryDetailAsync(url).Result;

            return Task.FromResult(EHentaiImageGalleryInfo.Create(client.EhUrl, detail));
        }

        public override async Task<GalleryImageDetail> GetImageDetial(GalleryItem item)
        {
            if ((item as EHentaiImageGalleryInfo)?.CachedGalleryDetail is GalleryImageDetail d)
                return d;

            if (!IdConverter.TryConvertBackEhentai(item.GalleryItemID, out var gid, out var token))
                return default;

            var url = client.EhUrl.GetGalleryDetailUrl(gid, token);
            var detail = Task.Run(async () => await client.GetGalleryDetailAsync(url)).Result;

            return await Task.FromResult(new EHentaiImageGalleryImageDetail(detail));
        }

        public override IAsyncEnumerable<GalleryItem> GetMainPostedImages() => SearchImagesAsync(default);

        public DetailImagePageBase GenerateDetailImagePageObject()
        {
            return new EHentaiGalleryDetailPage(client);
        }

        public async Task AccountLoginAsync(AccountInfo info)
        {
            try
            {
                var respUserName = await client.SignInAsync(info.Name, info.Password);
                if (respUserName?.Equals(info.Name, StringComparison.InvariantCultureIgnoreCase) ?? false)
                {
                    Log<EHentaiGallery>.Info("login successfully.");
                }
            }
            catch (Exception e)
            {
                Log<EHentaiGallery>.Error(e.Message);
            }
        }

        public Task AccountLogoutAsync()
        {
            //nothing to do.
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<GalleryItem> SearchImagesAsync(IEnumerable<string> keywords)
        {
            var page = 0;
            var urlBuilder = new ListUrlBuilder(client.EhUrl);

            if (keywords?.Count() > 0)
                urlBuilder.Keyword = string.Join(" ", keywords);

            while (true)
            {
                if (page != 0)
                    urlBuilder.PageIndex = page;

                var url = urlBuilder.Build();

                var result = await client.GetGalleryListAsync(url);
                foreach (var info in result.galleryInfoList)
                    yield return EHentaiImageGalleryInfo.Create(client.EhUrl, info);

                if (result.galleryInfoList.Count == 0)
                    break;

                page++;
            }
        }
    }
}
