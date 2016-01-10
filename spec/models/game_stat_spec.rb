# == Schema Information
#
# Table name: game_stats
#
#  id       :integer          not null, primary key
#  team_id  :integer
#  home     :boolean
#  decision :integer
#  goals    :integer
#  ppg      :integer
#  shg      :integer
#  evg      :integer
#  shots    :integer
#  pim      :integer
#  game_id  :integer
#
# Indexes
#
#  index_game_stats_on_game_id  (game_id)
#  index_game_stats_on_team_id  (team_id)
#

require 'rails_helper'

RSpec.describe GameStat, type: :model do
  pending "add some examples to (or delete) #{__FILE__}"
end
