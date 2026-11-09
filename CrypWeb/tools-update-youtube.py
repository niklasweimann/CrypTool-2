#!/usr/bin/env python3
"""Snapshots the CrypTool 2 YouTube channel feed into CrypWeb.App/wwwroot/youtube.json (the browser cannot read the feed directly: CORS).
Run it before publishing to refresh the Startcenter video list."""
import json, re, urllib.request, xml.etree.ElementTree as ET
URL = "https://www.youtube.com/feeds/videos.xml?channel_id=UC8_FqvQWJfZYxcSoEJ5ob-Q"
ns = {"a": "http://www.w3.org/2005/Atom", "y": "http://www.youtube.com/xml/schemas/2015"}
root = ET.fromstring(urllib.request.urlopen(urllib.request.Request(URL, headers={"User-Agent": "CrypTool 2/Startcenter"}), timeout=30).read())
items = [{"id": e.find("y:videoId", ns).text, "title": e.find("a:title", ns).text, "published": e.find("a:published", ns).text[:10]} for e in root.findall("a:entry", ns)]
json.dump(items, open("CrypWeb.App/wwwroot/youtube.json", "w"), ensure_ascii=False, indent=1)
print(len(items), "videos")
