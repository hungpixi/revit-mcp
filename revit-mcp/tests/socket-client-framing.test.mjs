import assert from "node:assert/strict";

import { extractJsonMessages } from "../build/utils/SocketClient.js";

const first = '{"jsonrpc":"2.0","id":"1","result":{"message":"ok"}}';
const second = '{"jsonrpc":"2.0","id":"2","result":{"message":"also ok"}}';

{
  const { messages, remaining } = extractJsonMessages(`${first}\n${second}\n`);

  assert.deepEqual(messages, [first, second]);
  assert.equal(remaining, "");
}

{
  const { messages, remaining } = extractJsonMessages(` \n\t${first}\n  ${second}`);

  assert.deepEqual(messages, [first, second]);
  assert.equal(remaining, "");
}

{
  const partial = '{"jsonrpc":"2.0","id":"3","result":';
  const { messages, remaining } = extractJsonMessages(`${first}\n${partial}`);

  assert.deepEqual(messages, [first]);
  assert.equal(remaining, partial);
}

{
  const withBracesInString =
    '{"jsonrpc":"2.0","id":"4","result":{"message":"literal } brace"}}';
  const { messages, remaining } = extractJsonMessages(
    `${withBracesInString}\n${second}`
  );

  assert.deepEqual(messages, [withBracesInString, second]);
  assert.equal(remaining, "");
}
