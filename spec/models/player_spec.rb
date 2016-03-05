# == Schema Information
#
# Table name: players
#
#  id       :integer          not null, primary key
#  name     :string
#  position :integer
#  key      :string
#  team_id  :integer
#
# Indexes
#
#  index_players_on_key  (key) UNIQUE
#

require 'rails_helper'

RSpec.describe Player, type: :model do
  pending "add some examples to (or delete) #{__FILE__}"
end
