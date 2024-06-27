// @ts-check
// Note: type annotations allow type checking and IDEs autocompletion

const lightCodeTheme = require('prism-react-renderer/themes/vsLight');
const darkCodeTheme = require('prism-react-renderer/themes/vsDark');
// @ts-ignore
const versions = require('./versions.json');

/** @type {import('@docusaurus/types').Config} */
const config = {
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

    presets: [
        [
            'classic',
            /** @type {import('@docusaurus/preset-classic').Options} */
            ({
                docs: {
                    sidebarPath: require.resolve('./sidebars.js'),
                    editUrl: "https://github.com/eventuous/docs/edit/master"
                },
                blog: {
                    showReadingTime: true,
                },
                theme: {
                    customCss: require.resolve('./src/css/custom.css'),
                },
            }),
        ],
    ],

    themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
        {
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
                        type: 'docSidebar',
                        sidebarId: 'connectorSidebar',
                        position: 'left',
                        label: 'Connector',
                    },
                    {
                        type: 'docsVersionDropdown',
                        position: 'right'
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
                                to: '/docs/connector/index',
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
                copyright: `Copyright © ${new Date().getFullYear()} Ubiquitous AS. Built with Docusaurus.`,
            },
            prism: {
                theme: lightCodeTheme,
                darkTheme: darkCodeTheme,
                additionalLanguages: ['csharp'],
            },
        },
};

module.exports = config;
