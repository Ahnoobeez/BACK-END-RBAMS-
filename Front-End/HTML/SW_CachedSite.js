const cacheName = 'v2';

self.addEventListener('install', e => {
    console.log('serviceworkerinstalled');
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
        fetch(e.request)
            .then(res =>{
                const resClone = res.clone();
                caches
                    .open(cacheName)
                    .then(cache => {
                        cache.put(e.request, resClone);
                    });
                return res;
            }).catch(err => caches.match(e.request).then(res => res))
    ); 
});