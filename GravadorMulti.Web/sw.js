/**
 * Service Worker para GravadorMulti Web
 * Permite funcionamento offline e cache de assets
 */

const CACHE_NAME = 'gravador-multi-v1';
const ASSETS_TO_CACHE = [
    '/',
    '/index.html',
    '/css/theme.css',
    '/css/styles.css',
    '/js/app.js',
    '/js/audio-service.js',
    '/js/project-service.js',
    '/js/ui-manager.js',
    '/js/waveform-utils.js',
    '/models/ItemRoteiro.js',
    '/models/Projeto.js',
    '/manifest.json'
];

// Instalação - Cache de assets
self.addEventListener('install', (event) => {
    event.waitUntil(
        caches.open(CACHE_NAME)
            .then((cache) => {
                console.log('Cache aberto:', CACHE_NAME);
                return cache.addAll(ASSETS_TO_CACHE);
            })
            .then(() => {
                console.log('Assets cacheados com sucesso');
                return self.skipWaiting();
            })
            .catch((error) => {
                console.error('Erro ao cachear assets:', error);
            })
    );
});

// Ativação - Limpeza de caches antigos
self.addEventListener('activate', (event) => {
    event.waitUntil(
        caches.keys()
            .then((cacheNames) => {
                return Promise.all(
                    cacheNames.map((cacheName) => {
                        if (cacheName !== CACHE_NAME) {
                            console.log('Removendo cache antigo:', cacheName);
                            return caches.delete(cacheName);
                        }
                    })
                );
            })
            .then(() => {
                console.log('Service Worker ativado');
                return self.clients.claim();
            })
    );
});

// Fetch - Estratégia Cache First, depois Network
self.addEventListener('fetch', (event) => {
    // Ignora requisições que não são GET
    if (event.request.method !== 'GET') {
        return;
    }

    // Ignora requisições de áudio do IndexedDB
    if (event.request.url.startsWith('blob:')) {
        return;
    }

    event.respondWith(
        caches.match(event.request)
            .then((cachedResponse) => {
                if (cachedResponse) {
                    // Retorna do cache
                    console.log('Servindo do cache:', event.request.url);
                    
                    // Atualiza cache em background (stale-while-revalidate)
                    fetch(event.request)
                        .then((networkResponse) => {
                            if (networkResponse && networkResponse.status === 200) {
                                const cacheClone = networkResponse.clone();
                                caches.open(CACHE_NAME)
                                    .then((cache) => {
                                        cache.put(event.request, cacheClone);
                                    });
                            }
                        })
                        .catch(() => {
                            // Network falhou, mas já temos o cache
                        });
                    
                    return cachedResponse;
                }

                // Não está no cache, busca da rede
                console.log('Buscando da rede:', event.request.url);
                return fetch(event.request)
                    .then((networkResponse) => {
                        if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
                            return networkResponse;
                        }

                        // Cacheia a resposta
                        const responseToCache = networkResponse.clone();
                        caches.open(CACHE_NAME)
                            .then((cache) => {
                                cache.put(event.request, responseToCache);
                            });

                        return networkResponse;
                    })
                    .catch((error) => {
                        console.error('Fetch falhou:', error);
                        
                        // Retorna fallback para HTML
                        if (event.request.headers.get('accept').includes('text/html')) {
                            return caches.match('/index.html');
                        }
                    });
            })
    );
});

// Mensagens do cliente
self.addEventListener('message', (event) => {
    if (event.data && event.data.type === 'SKIP_WAITING') {
        self.skipWaiting();
    }
    
    if (event.data && event.data.type === 'CLEAR_CACHE') {
        caches.delete(CACHE_NAME)
            .then(() => {
                console.log('Cache limpo sob demanda');
            });
    }
});
