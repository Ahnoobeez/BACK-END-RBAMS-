const cacheName = 'v1';

const cacheAssets = [
    '/Front-End/HTML/HTML.html',

    '/Front-End/main-content/analytics.html',
    '/Front-End/main-content/logistics.html',
    '/Front-End/main-content/marketing.html',
    '/Front-End/main-content/production.html',
    '/Front-End/main-content/technical.html',
    '/Front-End/main-content/warehouse.html',

    '/Front-End/Css/Admin.css',
    '/Front-End/Css/Css.css',
    '/Front-End/Css/Finance.css',
    '/Front-End/Css/Hr.css',
    '/Front-End/Css/Logistics.css',
    '/Front-End/Css/MarketingSales.css',
    '/Front-End/Css/Production.css',
    '/Front-End/Css/Technical.css',
    '/Front-End/Css/Warehouse.css',

    '/Front-End/JAVASCRIPT/API.js',
    '/Front-End/JAVASCRIPT/jcsript.js',
    '/Front-End/JAVASCRIPT/marketing.js',
    '/Front-End/JAVASCRIPT/production.js',
    '/Front-End/JAVASCRIPT/technical.js',
    '/Front-End/JAVASCRIPT/warehouse.js',

    '/Front-End/image/logo.png',
    '/Front-End/image/profile.jpg'
];


self.addEventListener('install', e => {
    console.log('serviceworkerinstalled');

    e.waitUntil(
        caches
        .open(cacheName)
        .then(cache => {
            console.log('Service Worker: Caching Files');
            cache.addAll(cacheAssets)
        })
        .then(() => self.skipWaiting())
    );

});

self.addEventListener('activate', e => {
    console.log('serviceworkeractivate');
    e.waitUntil(
        caches.keys().then(cacheNames => {
            return Promise.all(
                cacheNames.map(cache => {
                    if(cache != cacheName){
                        console.log('Service Worker Clearing Old Cache');
                        return caches.delete(cache);
                    }
                })
            )
        })
    );
});

self.addEventListener('fetch', e => {
    console.log('ServiceWorkerFetching');
    e.respondWith(
        fetch(e.request).catch(() => caches.match(e.request))
    ); 
});