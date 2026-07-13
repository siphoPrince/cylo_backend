-- Delete comments that don't have a valid post
DELETE FROM Comments WHERE PostId NOT IN (SELECT Id FROM Posts);

-- OR, if you don't care about existing test data, just wipe both
DELETE FROM Comments;
DELETE FROM Posts;