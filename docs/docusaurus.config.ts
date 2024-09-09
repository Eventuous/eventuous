import type {Config} from "@docusaurus/types";
import type * as Preset from "@docusaurus/preset-classic";
import {themes} from "prism-react-renderer";
import versions from "./versions.json";

const config: Config = {
    title: 'Eventuous',
    tagline: 'Event Sourcing for .NET',
    favicon: 'img/favicon.ico',

    url: 'https://eventuous.dev',
    baseUrl: '/',

    organizationName: 'eventuous',
    projectName: 'eventuous',

    onBrokenLinks: 'throw',
    onBrokenMarkdownLinks: 'warn',

    markdown: {
        mermaid: true,
    },
    themes: ['@docusaurus/theme-mermaid'],

    i18n: {
        defaultLocale: 'en',
        locales: ['en'],
    },

    presets: [[
        'classic',
        {
            docs: {
                sidebarPath: require.resolve('./sidebars.js'),
                editUrl: "https://github.com/eventuous/eventuous/docs/edit/master",
                includeCurrentVersion: false
            },
            blog: {
                showReadingTime: true,
            },
            theme: {
                customCss: require.resolve('./src/css/custom.css'),
            },
            sitemap: {
                lastmod: 'date',
                changefreq: 'weekly',
            }
        } satisfies Preset.Options,
    ]],

    themeConfig: {
        metadata: [{name: 'keywords', content: 'event sourcing, eventsourcing, dotnet, .NET, .NET Core'}],
        image: 'img/social-card.png',
        algolia: {
            appId: 'YQSSKN21VQ',
            apiKey: 'd62759f3b1948de19fea5476182dbd66',
            indexName: 'eventuous',
        },
        navbar: {
            title: 'Eventuous',
            logo: {
                alt: 'Eventuous logo',
                src: 'img/logo.png',
            },
            items: [
                {
                    type: 'doc',
                    docId: 'intro',
                    position: 'left',
                    label: 'Documentation',
                },
                {
                    type: 'docsVersionDropdown',
                    position: 'right',
                },
                {
                    href: 'https://github.com/sponsors/Eventuous',
                    position: 'right',
                    label: "Sponsor"
                },
                {
                    href: 'https://blog.eventuous.dev',
                    position: 'right',
                    label: "Blog"
                },
                {
                    href: 'https://github.com/eventuous/eventuous',
                    position: 'right',
                    className: 'header-github-link',
                    'aria-label': 'GitHub repository',
                },
            ],
        },
        footer: {
            style: 'dark',
            links: [
                {
                    title: 'Docs',
                    items: [
                        {
                            label: 'Documentation',
                            to: '/docs/intro',
                        },
                        {
                            label: 'Connector',
                            href: "https://connect.eventuous.dev"
                        },
                    ],
                },
                {
                    title: 'Community',
                    items: [
                        {
                            label: 'Discord',
                            href: 'https://discord.gg/ZrqM6vnnmf',
                        },
                    ],
                },
                {
                    title: 'More',
                    items: [
                        {
                            label: 'Blog',
                            href: 'https://blog.eventuous.dev',
                        },
                        {
                            label: 'GitHub',
                            href: 'https://github.com/eventuous/eventuous',
                        },
                    ],
                },
            ],
            copyright: `Copyright © ${new Date().getFullYear()} Eventuous HQ OÜ. Built with Docusaurus.`,
        },
        prism: {
            theme: themes.vsLight,
            darkTheme: themes.vsDark,
            additionalLanguages: ['csharp'],
        },
    } satisfies Preset.ThemeConfig,
};

module.exports = config;
