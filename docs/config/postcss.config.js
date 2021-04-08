const autoprefixer = require('autoprefixer');
const purgecss = require('@fullhuman/postcss-purgecss');
const whitelister = require('purgecss-whitelister');

module.exports = {
  plugins: [
    autoprefixer(),
    purgecss({
      content: [
        './layouts/**/*.html',
        './content/**/*.md',
      ],
      safelist: [
        'lazyloaded',
        'table',
        'thead',
        'tbody',
        'tr',
        'th',
        'td',
        ...whitelister([
          './assets/scss/components/_code.scss',
          './assets/scss/components/_search.scss',
          './assets/scss/components/_alerts.scss',
          './assets/scss/common/_dark.scss',
          './assets/scss/highlight/_syntax.scss',
          './assets/scss/highlight/_syntax-dark.scss',
        ]),
      ],
    }),
  ],
}
