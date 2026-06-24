// ============================================================
// 🍬👟 CANDY SHOES - SERVICE WORKER
// ============================================================

const CACHE_NAME = 'candy-shoes-v1';
const STATIC_CACHE = 'static-v1';
const DYNAMIC_CACHE = 'dynamic-v1';

// Archivos a cachear (static)
const STATIC_FILES = [
    '/',
    '/Home/Index',
    '/Tienda/Index',
    '/Account/Login',
    '/css/candy-shoes.css',
    '/manifest.json',
    // Iconos
    '/icons/icon-192x192.png',
    '/icons/icon-512x512.png',
    // Recursos externos
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css',
    'https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.5.0/css/all.min.css',
    'https://fonts.googleapis.com/css2?family=Poppins:wght@300;400;500;600;700;800&display=swap',
    'https://code.jquery.com/jquery-3.6.0.min.js',
    'https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js'
];

// ============================================================
// 📥 INSTALACIÓN
// ============================================================
self.addEventListener('install', event => {
    console.log('[Service Worker] Installing...');
    event.waitUntil(
        caches.open(STATIC_CACHE)
            .then(cache => {
                console.log('[Service Worker] Caching static files...');
                return cache.addAll(STATIC_FILES);
            })
            .then(() => {
                console.log('[Service Worker] Installation complete!');
                return self.skipWaiting();
            })
            .catch(err => {
                console.error('[Service Worker] Installation failed:', err);
            })
    );
});

// ============================================================
// 🔍 ACTIVACIÓN
// ============================================================
self.addEventListener('activate', event => {
    console.log('[Service Worker] Activating...');
    event.waitUntil(
        caches.keys()
            .then(keys => {
                return Promise.all(
                    keys.map(key => {
                        if (key !== STATIC_CACHE && key !== DYNAMIC_CACHE) {
                            console.log('[Service Worker] Removing old cache:', key);
                            return caches.delete(key);
                        }
                    })
                );
            })
            .then(() => {
                console.log('[Service Worker] Activation complete!');
                return self.clients.claim();
            })
    );
});

// ============================================================
// 🌐 INTERCEPTAR PETICIONES (Network First)
// ============================================================
self.addEventListener('fetch', event => {
    // Ignorar peticiones que no son GET
    if (event.request.method !== 'GET') {
        return;f
    }

    // IGNORAR PETICIONES DE ICONOS
    if (event.request.url.includes('/icons/')) {
        return;
    }

    // Ignorar peticiones a la API
    if (event.request.url.includes('/api/')) {
        return;
    }

    // Ignorar peticiones a Supabase
    if (event.request.url.includes('supabase')) {
        return;
    }
    if (event.request.url.includes('/manifest.json')) {
        return;
    }
    event.respondWith(
        // Estrategia: Network First (con fallback a cache)
        fetch(event.request)
            .then(response => {
                // Clonar la respuesta para cachearla
                const responseClone = response.clone();
                caches.open(DYNAMIC_CACHE)
                    .then(cache => {
                        cache.put(event.request, responseClone);
                    });
                return response;
            })
            .catch(() => {
                // Si falla la red, buscar en cache
                return caches.match(event.request)
                    .then(cachedResponse => {
                        if (cachedResponse) {
                            return cachedResponse;
                        }
                        // Si no está en cache, mostrar página offline
                        return caches.match('/offline.html');
                    });
            })
    );
});

// ============================================================
// 📩 SINCERONIZACIÓN EN SEGUNDO PLANO (opcional)
// ============================================================
self.addEventListener('sync', event => {
    console.log('[Service Worker] Sync event received:', event.tag);
});