const path = require('path');

module.exports = function (context, fromOptions) {
    const isProd = true; //process.env.NODE_ENV === 'production';

    return {
        name: 'segment',

        getClientModules() {
            return isProd ? [path.resolve(__dirname, './segment')] : [];
        },
    };
};