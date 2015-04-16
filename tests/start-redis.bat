cd ..\packages\Redis-64.2.8.17
redis-server.exe redis.windows.conf --maxheap 100mb
redis-cli.exe flushdb